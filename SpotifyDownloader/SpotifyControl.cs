using Microsoft.Practices.Unity;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SpotifyDownloader
{
    public partial class SpotifyControl : Form
    {
        private SpotifyWebAPI _spotify;
        private ImplicitGrantAuth _auth;

        private PrivateProfile _profile;
        private List<SimplePlaylist> _playlists;
        private List<PlaylistTrack> _traks;

        private UnityContainer _unityContainer;
        private IDownloader _downloader;
        
        private bool Canceling = false;

        static string ClientId = "";    // client id from spotify api

        public SpotifyControl()
        {
            InitializeComponent();

            _auth = new ImplicitGrantAuth
            {
                RedirectUri = "http://localhost:8000",
                ClientId = ClientId,
                Scope = Scope.UserReadPrivate | Scope.UserReadEmail | Scope.PlaylistReadPrivate | Scope.UserLibrarayRead | Scope.UserReadPrivate | Scope.UserFollowRead | Scope.UserReadBirthdate,
                State = "XSS"
            };
            _auth.OnResponseReceivedEvent += _auth_OnResponseReceivedEvent;

            _unityContainer = new UnityContainer();
            _unityContainer.RegisterType<IDownloader, YouTubeDownloader>();
            
        }

        private void _auth_OnResponseReceivedEvent(Token token, string state)
        {
            _auth.StopHttpServer();

            if (state != "XSS")
            {
                MessageBox.Show(@"Wrong state received.", @"SpotifyWeb API Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (token.Error != null)
            {
                MessageBox.Show($"Error: {token.Error}", @"SpotifyWeb API Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _spotify = new SpotifyWebAPI
            {
                UseAuth = true,
                AccessToken = token.AccessToken,
                TokenType = token.TokenType
            };
            InitialSetup();
        }

        private async void InitialSetup()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(InitialSetup));
                return;
            }

            authButton.Enabled = false;
            _profile = _spotify.GetPrivateProfile();

            _playlists = GetPlaylists();
            playlistsCountLabel.Text = _playlists.Count.ToString();
            _playlists.ForEach(playlist => playlistsListBox.Items.Add(playlist.Name));

            displayNameLabel.Text = _profile.DisplayName;
            countryLabel.Text = _profile.Country;
            emailLabel.Text = _profile.Email;
            accountLabel.Text = _profile.Product;

            if (_profile.Images != null && _profile.Images.Count > 0)
            {
                using (WebClient wc = new WebClient())
                {
                    byte[] imageBytes = await wc.DownloadDataTaskAsync(new Uri(_profile.Images[0].Url));
                    using (MemoryStream stream = new MemoryStream(imageBytes))
                        avatarPictureBox.Image = System.Drawing.Image.FromStream(stream);
                }
            }

            this.buttonSelectFolder.Enabled = true;
        }

        private List<SimplePlaylist> GetPlaylists()
        {
            Paging<SimplePlaylist> playlists = _spotify.GetUserPlaylists(_profile.Id);
            List<SimplePlaylist> list = playlists.Items.ToList();

            while (playlists.Next != null)
            {
                playlists = _spotify.GetUserPlaylists(_profile.Id, 20, playlists.Offset + playlists.Limit);
                list.AddRange(playlists.Items);
            }

            return list;
        }

        private void authButton_Click(object sender, EventArgs e)
        {
            _auth.StartHttpServer(8000);
            _auth.DoAuth();
        }

        private void buttonDownload_Click(object sender, EventArgs e)
        {
            if (this.buttonDownload.Text == "Descargar")
            {

                _downloader = _unityContainer.Resolve<IDownloader>();

                this.playlistsListBox.Enabled = false;
                this.buttonSelectFolder.Enabled = false;
                this.buttonDownload.Text = "Cancelar";

                int selectedIndex = this.playlistsListBox.SelectedIndex;
                var playlistSelected = _playlists[selectedIndex];


                string carpetaSeleccionada = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                if (!string.IsNullOrEmpty(this.textBoxSelectedFolder.Text))
                {
                    carpetaSeleccionada = this.textBoxSelectedFolder.Text;
                }

                string ruta = Path.Combine(carpetaSeleccionada, Create_Valid_FileName(playlistSelected.Name));

                if (!Directory.Exists(ruta))
                {
                    Directory.CreateDirectory(ruta);
                }

                BackgroundWorker bgMan = new BackgroundWorker();
                
                int cont = 0;
                int total = _traks.Count;
                
                foreach (var song in _traks)
                {
                    if (!this.Canceling)
                    {
                        this.labelDownloadState.Text = song.Track.Name;
                        this.labelDownloadState.Visible = true;
                        this.progressBarDownloadsong.Value = 0;
                        Application.DoEvents();

                        _downloader.Configure(Path.Combine(ruta, Create_Valid_FileName(song.Track.Name)), song.Track.Name, song.Track.Artists[0].Name);

                        if (_downloader.ExistSong())
                        {
                            bgMan.Dispose();
                            bgMan = new BackgroundWorker();
                            bgMan.WorkerReportsProgress = true;
                            bgMan.DoWork += (s, args) => _downloader.Download(bgMan);
                            bgMan.ProgressChanged += (s, args) => YouAPI_sendingProgress(args.ProgressPercentage);
                            bgMan.RunWorkerAsync();
                            this.progressBarDownloadsong.Style = ProgressBarStyle.Marquee;
                            while (bgMan.IsBusy)
                            {
                                Application.DoEvents();
                            }
                        }
                        else
                        {
                            File.Create(Path.Combine(ruta, song.Track.Name) + ".mp3_NotFound");
                        }
                        cont++;
                        this.progressBarDownload.Value = cont * 100 / total;
                        this.progressBarDownloadsong.Value = this.progressBarDownloadsong.Maximum;
                        Application.DoEvents();
                    }
                    else
                    {
                        Canceling = false;
                        break;
                    }
                }

                StopDownload();
                
            }
            else
            {
                this.buttonDownload.Enabled = false;
                this.buttonDownload.Text = "Cancelando...";
                Canceling = true;
            }
        }

        private void StopDownload()
        {
            this.labelDownloadState.Visible = false;
            this.progressBarDownload.Value = 0;
            this.progressBarDownloadsong.Style = ProgressBarStyle.Continuous;
            this.progressBarDownloadsong.Value = 0;
            this.playlistsListBox.Enabled = true;
            this.buttonSelectFolder.Enabled = true;

            this.buttonDownload.Text = "Descargar";
        }


        private string Create_Valid_FileName(string fileName)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        private void YouAPI_sendingProgress(double Progress)
        {
            if (this.progressBarDownloadsong.Style == ProgressBarStyle.Marquee)
            {
                this.progressBarDownloadsong.Style = ProgressBarStyle.Continuous;
            }
            this.progressBarDownloadsong.Value = Convert.ToInt32(Progress);
        }

        private void playlistsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int selectedIndex = this.playlistsListBox.SelectedIndex;
            var playlistSelected = _playlists[selectedIndex];

            if (_traks == null)
            {
                _traks = new List<PlaylistTrack>();
            }
            else
            {
                _traks.Clear();
            }
            this.listBox1.Items.Clear();

            // Getting the songs
            var songs = _spotify.GetPlaylistTracks(_profile.Id, playlistSelected.Id);
            int cont = 0;
            while (cont < songs.Total)
            {
                foreach (var song in songs.Items)
                {
                    _traks.Add(song);
                    this.listBox1.Items.Add(song.Track.Name);
                }
                cont += songs.Items.Count;
                songs = _spotify.GetPlaylistTracks(_profile.Id, playlistSelected.Id);
            }

            this.buttonDownload.Enabled = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.folderBrowserDialog1.ShowDialog();

            this.textBoxSelectedFolder.Text = this.folderBrowserDialog1.SelectedPath;
        }
    }
}
