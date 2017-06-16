/*
 * Copyright 2015 Google Inc. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *
 */

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System.Collections.Generic;

namespace Tuber
{
    /// <summary>
    /// YouTube Data API v3 sample: create a playlist.
    /// Relies on the Google APIs Client Library for .NET, v1.7.0 or higher.
    /// See https://code.google.com/p/google-api-dotnet-client/wiki/GettingStarted
    /// </summary>
    internal class Tuber
    {
        const string PLAYLIST_NAME = "gamegrumps";

        // To find the channel ID given a legacy username, in this case "gamegrumps" (or view source and search for "uploads" on the channel's page):
        // https://www.googleapis.com/youtube/v3/channels?key={YOUR_API_KEY}&forUsername=gamegrumps&part=id
        // https://www.googleapis.com/youtube/v3/channels?key=AIzaSyC9PWHciAV7LV_koodnvA6a3r8bGbHI94U&forUsername=gamegrumps&part=contentDetails
        const string CHANNEL_ID = "UU9CuvdOVfMPvKCiwdGKL3cQ";

        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("YouTube Data API: Playlist Updates");
            Console.WriteLine("==================================");

            try
            {
                new Tuber().Run().Wait();
            }
            catch (AggregateException ex)
            {
                foreach (var e in ex.InnerExceptions)
                {
                    Console.WriteLine("Error: " + e.Message);
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private async Task Run()
        {
            UserCredential credential;
            using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.Load(stream).Secrets,
                // This OAuth 2.0 access scope allows for full read/write access to the
                // authenticated user's account.
                new[] { YouTubeService.Scope.Youtube },
                "user",
                CancellationToken.None,
                new FileDataStore(this.GetType().ToString())
                );
            }

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = this.GetType().ToString()
            });
            
            // Get the user's playlist to edit
            var playlist = await getUserPlaylist(youtubeService, PLAYLIST_NAME);

            // Figure out where to stop searching for videos on the channel
            var stopAtVideoId = await getStopAtVideoId(youtubeService, playlist);

            // Get the channel's playlist
            var channelUploads = await getChannelUploads(youtubeService, CHANNEL_ID, stopAtVideoId);

            channelUploads.Sort((x, y) => (x.PublishedAt.CompareTo(y.PublishedAt)));

            Console.WriteLine("Sorted:");
            foreach (var video in channelUploads)
            {
                Console.WriteLine(video);
            }

            removeWatched(youtubeService, playlist);

            addVideosToPlaylist(youtubeService, channelUploads, playlist, stopAtVideoId);

            // Add a video to the newly created playlist.
            /*var newPlaylistItem = new PlaylistItem();
            newPlaylistItem.Snippet = new PlaylistItemSnippet();
            newPlaylistItem.Snippet.PlaylistId = newPlaylist.Id;
            newPlaylistItem.Snippet.ResourceId = new ResourceId();
            newPlaylistItem.Snippet.ResourceId.Kind = "youtube#video";
            newPlaylistItem.Snippet.ResourceId.VideoId = "GNRMeaz6QRI";
            newPlaylistItem = await youtubeService.PlaylistItems.Insert(newPlaylistItem, "snippet").ExecuteAsync();

            Console.WriteLine("Playlist item id {0} was added to playlist id {1}.", newPlaylistItem.Id, newPlaylist.Id);
            */
        }

        public void removeWatched(YouTubeService youtubeService, Playlist playlist)
        {
            
        }

        public async void addVideosToPlaylist(YouTubeService youtubeService, List<Video> videosToAdd, Playlist playlist, String startAfterVideoId)
        {
            Boolean afterVideoId = false;
            foreach (Video video in videosToAdd)
            {
                if (afterVideoId)
                {
                    // Add the specified videos to the given playlist
                    var newPlaylistItem = new PlaylistItem();
                    newPlaylistItem.Snippet = new PlaylistItemSnippet();
                    newPlaylistItem.Snippet.PlaylistId = playlist.Id;
                    newPlaylistItem.Snippet.ResourceId = new ResourceId();
                    newPlaylistItem.Snippet.ResourceId.Kind = "youtube#video";
                    newPlaylistItem.Snippet.ResourceId.VideoId = video.VideoId;
                    newPlaylistItem = await youtubeService.PlaylistItems.Insert(newPlaylistItem, "snippet").ExecuteAsync();
                } else if (video.VideoId.Equals(startAfterVideoId))
                {
                    afterVideoId = true;
                }
            }

        }

        public async Task<DateTime> getPublishedAt(YouTubeService youtubeService, String videoId)
        {
            VideosResource.ListRequest videosListRequest = youtubeService.Videos.List("snippet, contentDetails");
            videosListRequest.Id = videoId;
            VideoListResponse videosListResponse = await videosListRequest.ExecuteAsync();
            DateTime publishedAt = (DateTime)videosListResponse.Items[0].Snippet.PublishedAt;
            return publishedAt;
        }

        private async Task<string> getStopAtVideoId(YouTubeService youtubeService, Playlist playlist)
        {
            var playlistItemsRequest = youtubeService.PlaylistItems.List("contentDetails,snippet,id");
            playlistItemsRequest.PlaylistId = playlist.Id;
            playlistItemsRequest.MaxResults = 50;
            string pageToken = null;

            while (true)
            {
                playlistItemsRequest.PageToken = pageToken;
                var playlistItemsResponse = await playlistItemsRequest.ExecuteAsync();
                pageToken = playlistItemsResponse.NextPageToken;

                if (pageToken == null)
                {
                    // We've reached the last page. Return the last video id.
                    return playlistItemsResponse.Items[playlistItemsResponse.Items.Count - 1].Snippet.ResourceId.VideoId;
                }
            }
        }

        private async Task<List<Video>> getChannelUploads(YouTubeService youtubeService, string channelID, string stopAtVideoId, string pageToken = null, int iteration = 1)
        {
            var playlistVideos = new List<Video>();
            
            // Find the channel upload playlist id
            var playlistItemsRequest = youtubeService.PlaylistItems.List("contentDetails,snippet");
            playlistItemsRequest.PlaylistId = channelID;
            playlistItemsRequest.MaxResults = 50;
            if (pageToken != null)
            {
                playlistItemsRequest.PageToken = pageToken;
            }

            var playlistItemsResponse = await playlistItemsRequest.ExecuteAsync();

            // Add a new Video object for each returned Video. 
            // Add all the videos; don't pay attention to the 
            // stopAtVideoId because these aren't ordered by upload 
            // date yet and we don't want to accidentally miss some.
            foreach (var item in playlistItemsResponse.Items)
            {
                String videoId = item.Snippet.ResourceId.VideoId;
                DateTime publishedAt = await getPublishedAt(youtubeService, videoId);
                Video video = new Video(videoId, item.Snippet.Title, publishedAt);
                playlistVideos.Add(video);
            }

            Console.WriteLine("{0} items. First item: {1}", playlistVideos.Count, playlistVideos[0]);
            foreach (var video in playlistVideos)
            {
                // If we've reached the Stop At video, return early
                if (video.VideoId.Equals(stopAtVideoId)) {
                    return playlistVideos;
                }
            }
            
            if (playlistItemsResponse.NextPageToken != null)
            {
                playlistVideos.AddRange(await getChannelUploads(youtubeService, channelID, stopAtVideoId, playlistItemsResponse.NextPageToken, iteration++));
            }

            return playlistVideos;
        }

        private async Task<Playlist> getUserPlaylist(YouTubeService youtubeService, string playlistName)
        {
            var playlistListRequest = youtubeService.Playlists.List("contentDetails,snippet");
            playlistListRequest.Mine = true;

            var playlistListResponse = new PlaylistListResponse();

            playlistListResponse = await playlistListRequest.ExecuteAsync();

            foreach (var playlistItem in playlistListResponse.Items)
            {
                if (playlistItem.Snippet.Title.ToLower().Equals(playlistName.ToLower()))
                {
                    Console.WriteLine("Found Playlist {0}", playlistItem.Snippet.Title);
                    return playlistItem;
                }
            }

            // No playlist was found. Create one.
            return await createPlaylist(youtubeService, playlistName);
        }

        /// <summary>
        /// Create a playlist with the given name
        /// </summary>
        /// <param name="youtubeService"></param>
        /// <param name="playlistName"></param>
        /// <returns></returns>
        private async Task<Playlist> createPlaylist(YouTubeService youtubeService, string playlistName)
        {
            // Create a new, private playlist in the authorized user's channel.
            Console.WriteLine("Creating Playlist {0}", PLAYLIST_NAME);
            var newPlaylist = new Playlist();
            newPlaylist.Snippet = new PlaylistSnippet();
            newPlaylist.Snippet.Title = playlistName;
            newPlaylist.Snippet.Description = "A playlist created with the YouTube API v3";
            newPlaylist.Status = new PlaylistStatus();
            newPlaylist.Status.PrivacyStatus = "public";
            newPlaylist = await youtubeService.Playlists.Insert(newPlaylist, "snippet,status").ExecuteAsync();
            return newPlaylist;
        }
    }
}
