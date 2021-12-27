using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyDownloader
{
    
    public interface IDownloader
    {    
        void Configure(params string[] Params);
        void Download(BackgroundWorker BgWorker);
        bool ExistSong();
    }
}
