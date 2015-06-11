using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Microsoft.Band.Portable;
using Microsoft.Band.Portable.Notifications;
using Microsoft.Band.Portable.Personalization;
using Microsoft.Band.Portable.Sensors;
using Microsoft.Band.Portable.Tiles;
using BandException = Microsoft.Band.BandException;
using Windows.UI.Xaml.Media.Imaging;

namespace FlightBand.Phone
{
    public sealed partial class MainPage : Page
    {
        private readonly FlightBandConnection bandConnection;
        private readonly FlightBandGame game;

        private readonly List<Image> enemies;
        private readonly List<Image> bullets;
        private Image player;

        public MainPage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Required;

            bandConnection = new FlightBandConnection();
            game = new FlightBandGame(bandConnection);

            enemies = new List<Image>();
            bullets = new List<Image>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            CompositionTarget.Rendering += OnRenderFrame;

            base.OnNavigatedTo(e);

            var manager = BandClientManager.Instance;
            var pairedBands = await manager.GetPairedBandsAsync();
            var bandInfo = pairedBands.FirstOrDefault();
            if (bandInfo != null)
            {
                await bandConnection.SetBandAsync(bandInfo);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CompositionTarget.Rendering -= OnRenderFrame;

            base.OnNavigatedFrom(e);
        }

        private void OnRenderFrame(object sender, object e)
        {
            // update frame

            var count = 0;
            var fps = game.Update();
            fpsTextBlock.Text = string.Format("FPS: {0:0.0}", fps);

            // update visual elements

            // the player
            if (game.Player != null)
            {
                if (player == null)
                {
                    player = new Image { Source = playerSprite.Source };
                    canvas.Children.Add(player);
                }

                velocityTextBlock.Text = string.Format(
                    "{0:0.00}, {1:0.00}", 
                    game.Player.HorizontalVelocity.Velocity,
                    game.Player.ForwardVelocity.Velocity);

                MoveObject(player, game.Player);
            }

            // the enemies
            count = game.Enemies.Length;
            CreateElements(count, enemies, enemySprite.Source);
            for (int i = 0; i < count; i++)
            {
                MoveObject(enemies[i], game.Enemies[i]);
            }

            // the bullets
            count = game.Bullets.Length;
            CreateElements(count, bullets, enemySprite.Source);
            for (int i = 0; i < count; i++)
            {
                MoveObject(bullets[i], game.Bullets[i]);
            }
        }

        private void CreateElements(int count, List<Image> elements, ImageSource sprite)
        {
            while (count > elements.Count)
            {
                var enemy = new Image { Source = sprite };
                elements.Add(enemy);
                canvas.Children.Add(enemy);
            }
            while (count < elements.Count)
            {
                var enemy = elements[elements.Count - 1];
                elements.Remove(enemy);
                canvas.Children.Remove(enemy);
            }
        }

        private void MoveObject(Image sprite, FlightBandMovingObject movingObject)
        {
            Canvas.SetLeft(sprite, movingObject.X);
            Canvas.SetTop(sprite, movingObject.Y);
            sprite.Width = movingObject.Width;
            sprite.Height = movingObject.Height;
        }

        private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (game != null)
            {
                game.ViewportWidth = e.NewSize.Width;
                game.ViewportHeight = e.NewSize.Height;
                game.PlaneScale = Math.Min(e.NewSize.Width, e.NewSize.Height) / 5.0;
                game.BulletScale = game.PlaneScale / 5.0;
            }
        }

        private void OnPlayButtonClicked(object sender, RoutedEventArgs e)
        {
            if (game != null)
            {
                game.Start();

                menu.Visibility = Visibility.Collapsed;
                canvas.Visibility = Visibility.Visible;
            }
        }
    }
}
