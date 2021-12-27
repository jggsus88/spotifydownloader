using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//using Google.Apis.Samples.Helper;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using YoutubeExtractor;
using System.IO;
using System.ComponentModel;

namespace SpotifyDownloader
{
    public class YouTubeDownloader : IDownloader
    {
        static string YouTubeApiKey = "";
        static string YooTubeUrl = "https://www.youtube.com/watch?v=";
        
        private string VideoId;
        private string UrlDest;

        #region Interfaz

        public void Configure(params string[] Params)
        {
            this.UrlDest = Params[0];

            string Song = Params[1];
            string Artist = Params[2];

            this.VideoId = Search(Song, Artist);
        }

        public void Download(BackgroundWorker bgWorker)
        {
            
            string link = YooTubeUrl + this.VideoId;
            IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(link);
            
            VideoInfo video = videoInfos
                .Where(info => info.CanExtractAudio)
                .OrderByDescending(info => info.AudioBitrate)
                .First();

            if (video.RequiresDecryption)
            {
                DownloadUrlResolver.DecryptDownloadUrl(video);
            }

            var audioDownloader = new AudioDownloader(video, this.UrlDest + video.AudioExtension);

            // Register the progress events. We treat the download progress as 85% of the progress and the extraction progress only as 15% of the progress,
            // because the download will take much longer than the audio extraction.
            audioDownloader.DownloadProgressChanged += (sender, args) => bgWorker.ReportProgress(Convert.ToInt16(args.ProgressPercentage * 0.85));
            audioDownloader.AudioExtractionProgressChanged += (sender, args) => bgWorker.ReportProgress(Convert.ToInt16(85 + args.ProgressPercentage * 0.15));

            audioDownloader.Execute();

            
        }


        public bool ExistSong()
        {
            return this.VideoId != null;
        }


        #endregion

        #region Metodos privados

        private string Search(string Song, string Artist)
        {
            string res = null;

            Google.Apis.YouTube.v3.YouTubeService youtube = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = YouTubeApiKey,
            });

            SearchResource.ListRequest listRequest = youtube.Search.List("snippet");
            listRequest.Q = Song + " " + Artist;
            listRequest.MaxResults = 1;
            listRequest.Type = "video";

            SearchListResponse searchResponse = listRequest.Execute();

            if (searchResponse.Items.Count > 0)
            {
                res = searchResponse.Items[0].Id.VideoId;
            }

            return res;
        }

        #endregion

    }
}
