using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Band.Portable;
using Microsoft.Band.Portable.Notifications;
using Microsoft.Band.Portable.Personalization;
using Microsoft.Band.Portable.Sensors;
using Microsoft.Band.Portable.Tiles;
using BandException = Microsoft.Band.BandException;
using Windows.Foundation;

namespace FlightBand
{
    public class FlightBandConnection
    {
        private BandClient bandClient;

        public bool HasConnection { get; set; }

        public double RotationX { get; private set; }
        public double RotationY { get; private set; }

        public async Task SetBandAsync(BandDeviceInfo bandInfo)
        {
            HasConnection = false;

            await DisconnectAsync();
            await ConnectAsync(bandInfo);

            HasConnection = true;
        }

        private async Task DisconnectAsync()
        {
            if (bandClient != null)
            {
                var accelerometer = bandClient.SensorManager.Accelerometer;
                await accelerometer.StopReadingsAsync();
                accelerometer.ReadingChanged -= OnReadingChanged;
                await bandClient.DisconnectAsync();
            }
        }

        private async Task ConnectAsync(BandDeviceInfo bandInfo)
        {
            bandClient = await BandClientManager.Instance.ConnectAsync(bandInfo);
            var accelerometer = bandClient.SensorManager.Accelerometer;
            accelerometer.ReadingChanged += OnReadingChanged;
            await accelerometer.StartReadingsAsync(BandSensorSampleRate.Ms128);
        }

        private void OnReadingChanged(object sender, BandSensorReadingEventArgs<BandAccelerometerReading> args)
        {
            var reading = args.SensorReading;

            if (reading.AccelerationX == 0 && reading.AccelerationY == 0) return; // todo: bug in WP SDK

            // get the rotation in degrees
            RotationX = reading.AccelerationX;
            RotationY = reading.AccelerationY;

            System.Diagnostics.Debug.WriteLine(
                "OnReadingChanged({0:0.00}, {1:0.00}); - {2}",
                reading.AccelerationX, reading.AccelerationY, Environment.TickCount);
        }
    }

    public class FlightBandGame
    {
        private const int MaximumEnemies = 3;
        private const double RespawnRate = 1.5;

        private readonly List<FlightBandEnemy> enemies;
        private readonly List<FlightBandBullet> bullets;
        private readonly FlightBandConnection bandConnection;
        private readonly Random random;

        private int lastUpdateTimestamp;
        private TimeSpan lastEnemyGenerationDelta;

        public FlightBandGame(FlightBandConnection connection)
        {
            bandConnection = connection;
            lastUpdateTimestamp = 0;
            lastEnemyGenerationDelta = TimeSpan.Zero;
            ViewportWidth = 0;
            ViewportHeight = 0;
            enemies = new List<FlightBandEnemy>();
            bullets = new List<FlightBandBullet>();
            Player = new FlightBandPlayer();
            random = new Random();
            Score = 0;
            State = GameState.NotStarted;
        }

        public const int StartingTimeout = 5;
        public const int FinishingTimeout = 5;

        public double ViewportWidth { get; set; }
        public double ViewportHeight { get; set; }
        public double PlaneScale { get; set; }
        public double BulletScale { get; set; }

        public FlightBandPlayer Player { get; private set; }
        public FlightBandEnemy[] Enemies { get { return enemies.ToArray(); } }
        public FlightBandBullet[] Bullets { get { return bullets.ToArray(); } }

        public int Score { get; private set; }

        public GameState State { get; private set; }

        public void Start()
        {
            lastUpdateTimestamp = Environment.TickCount;
            lastEnemyGenerationDelta = TimeSpan.Zero;
            Score = 0;

            CreateGameLayout();

            State = GameState.Starting;
        }

        public double Update()
        {
            var currentTimestamp = Environment.TickCount;
            var dtMs = currentTimestamp - lastUpdateTimestamp;
            var deltaTime = TimeSpan.FromMilliseconds(dtMs);

            // update the game frame
            if (State == GameState.NotStarted)
            {
            }
            else if (State == GameState.Starting)
            {
                State = GameState.Started;
            }
            else if (State == GameState.Finishing)
            {
            }
            else if (State == GameState.Paused || !bandConnection.HasConnection)
            {
                if (State == GameState.Paused)
                {

                }
                if (!bandConnection.HasConnection)
                {

                }
            }
            else if (State == GameState.Started)
            {
                UpdateObjects(deltaTime);
            }

            lastUpdateTimestamp = currentTimestamp;
            return 1000.0 / dtMs;
        }

        private void CreateGameLayout()
        {
            // reset
            enemies.Clear();
            bullets.Clear();
            Player.Reset();

            // recreate player
            Player.Width = PlaneScale;
            Player.Height = PlaneScale;
            Player.X = (ViewportWidth - PlaneScale) / 2.0;
            var halfHeight = ViewportHeight / 2.0;
            Player.Y = ((halfHeight - PlaneScale) / 2.0) + halfHeight;
            Player.MovementBounds = new Rect(0, halfHeight, ViewportWidth - PlaneScale, halfHeight - PlaneScale);
        }

        private void UpdateObjects(TimeSpan deltaTime)
        {
            // update the player
            Player.ApplyInput(bandConnection.RotationX, bandConnection.RotationY);
            Player.Update(deltaTime);
            if (Player.IsFiring)
            {
                var bullet = new FlightBandBullet
                {
                    Width = BulletScale,
                    Height = BulletScale,
                    X = Player.X + Player.Width / 2.0,
                    Y = Player.Y,
                };
                bullet.ForwardVelocity.Velocity = -bullet.ForwardVelocity.MaximumVelocity;
                bullets.Add(bullet);
            }

            // create any new enemies
            if (enemies.Count <= MaximumEnemies)
            {
                lastEnemyGenerationDelta += deltaTime;
                if (lastEnemyGenerationDelta.TotalSeconds >= RespawnRate)
                {
                    lastEnemyGenerationDelta = TimeSpan.Zero;
                    var newEnemy = new FlightBandEnemy
                    {
                        Width = PlaneScale,
                        Height = PlaneScale,
                        X = random.Next((int)(ViewportWidth - PlaneScale)),
                        Y = -PlaneScale - random.Next((int)PlaneScale),
                    };
                    newEnemy.ForwardVelocity.Velocity = random.Next(
                        (int)newEnemy.ForwardVelocity.MinimumVelocity,
                        (int)newEnemy.ForwardVelocity.MaximumVelocity);
                    enemies.Add(newEnemy);
                }
            }

            // update the enemies
            foreach (var enemy in Enemies)
            {
                enemy.Update(deltaTime);
                if (enemy.Y > ViewportHeight)
                {
                    enemies.Remove(enemy);
                    // todo: some scoring for missed planes
                }
                else
                {
                    if (enemy.IsDead)
                    {
                        enemies.Remove(enemy);
                        // todo: some scoring for killed planes
                    }
                    else if (enemy.IsFiring)
                    {
                        var bullet = new FlightBandBullet
                        {
                            Width = BulletScale,
                            Height = BulletScale,
                            X = enemy.X + enemy.Width / 2.0,
                            Y = enemy.Y + PlaneScale,
                        };
                        bullet.ForwardVelocity.Velocity = bullet.ForwardVelocity.MaximumVelocity;
                        bullets.Add(bullet);
                    }
                }
            }

            // update the bullets
            foreach (var bullet in Bullets)
            {
                // move them
                bullet.Update(deltaTime);

                if (bullet.Y + bullet.Height < 0 || bullet.IsDestroyed)
                {
                    bullets.Remove(bullet);
                }
                else
                {
                    // collision detection
                    Player.ApplyCollision(bullet);
                    foreach (var enemy in enemies)
                    {
                        enemy.ApplyCollision(bullet);
                    }
                }
            }
        }
    }

    public enum GameState
    {
        NotStarted,

        Starting,
        Started,
        Paused,
        Finishing
    }

    public class FlightBandPlayer : FlightBandPlane
    {
        private const double AngleForFullSpeed = 90.0;

        public FlightBandPlayer()
        {
            ForwardVelocity.MaximumVelocity = 200;
            ForwardVelocity.MinimumVelocity = -200;
            HorizontalVelocity.MaximumVelocity = 200;
            HorizontalVelocity.MinimumVelocity = -200;
        }
        
        // lives

        protected const int MaximumLives = 3;

        public int Lives { get; set; }

        public void Reset()
        {
            Lives = MaximumLives;

            Health = MaximumHealth;
            LastFireDelta = TimeSpan.Zero;

            ForwardVelocity.Acceleration = 0;
            ForwardVelocity.Velocity = 0;
            HorizontalVelocity.Acceleration = 0;
            HorizontalVelocity.Velocity = 0;
        }

        public virtual void ApplyInput(double rotationX, double rotationY)
        {
            ForwardVelocity.ApplyForce(-rotationX * 3.0 * ForwardVelocity.MaximumVelocity);
            HorizontalVelocity.ApplyForce(-rotationY * 3.0 * HorizontalVelocity.MaximumVelocity);
        }
    }

    public class FlightBandEnemy : FlightBandPlane
    {
        public const int ScoreValue = 1;

        public FlightBandEnemy()
        {
            ForwardVelocity.MinimumVelocity = 100;
            ForwardVelocity.MaximumVelocity = 120;
            HorizontalVelocity.MinimumVelocity = 0;
            HorizontalVelocity.MaximumVelocity = 0;
        }
    }

    public abstract class FlightBandPlane : FlightBandMovingObject
    {
        // health

        protected const int MaximumHealth = 100;
        public int Health { get; set; }

        // weapons

        protected const double FireRate = 0.5; // 2 times per second
        public TimeSpan LastFireDelta { get; set; }
        public bool IsFiring { get; set; }
        public bool IsHit { get; set; }
        public bool IsDead { get; set; }

        // collision

        public virtual void ApplyCollision(FlightBandBullet bullet)
        {
            if (bullet.X > X &&
                bullet.Y > Y &&
                bullet.X + bullet.Width < X + Width &&
                bullet.Y + bullet.Height < Y + Height)
            {
                IsHit = true;
                Health -= bullet.Damage;
                if (Health <= 0)
                {
                    IsDead = true;
                }
                bullet.IsDestroyed = true;
            }
        }

        public override void Update(TimeSpan deltaTime)
        {
            base.Update(deltaTime);

            LastFireDelta += deltaTime;
            IsFiring = LastFireDelta.TotalSeconds >= FireRate;
            if (IsFiring)
            {
                LastFireDelta = TimeSpan.Zero;
            }
        }
    }

    public class FlightBandBullet : FlightBandMovingObject
    {
        public FlightBandBullet()
        {
            ForwardVelocity.MinimumVelocity = -200;
            ForwardVelocity.MaximumVelocity = 200;
            HorizontalVelocity.MinimumVelocity = 0;
            HorizontalVelocity.MaximumVelocity = 0;

            Damage = 25;
        }

        // weapons

        public int Damage { get; set; }
        public bool IsDestroyed { get; set; }
    }

    public class FlightBandMovingObject
    {
        public FlightBandMovingObject()
        {
            ForwardVelocity = new VelocityTracker();
            HorizontalVelocity = new VelocityTracker();
            MovementBounds = Rect.Empty;
        }

        // position & size

        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        // movement

        public Rect MovementBounds { get; set; }

        public VelocityTracker ForwardVelocity { get; private set; }
        public VelocityTracker HorizontalVelocity { get; private set; }

        public virtual void Update(TimeSpan deltaTime)
        {
            var secondsElapsed = deltaTime.TotalSeconds;

            ForwardVelocity.Update(deltaTime);
            HorizontalVelocity.Update(deltaTime);

            var yValue = Y + ForwardVelocity.Velocity * secondsElapsed;
            var xValue = X + HorizontalVelocity.Velocity * secondsElapsed;

            if (MovementBounds.IsEmpty)
            {
                Y = yValue;
                X = xValue;
            }
            else
            {
                Y = MathUtils.Clamp(MovementBounds.Top, yValue, MovementBounds.Bottom);
                X = MathUtils.Clamp(MovementBounds.Left, xValue, MovementBounds.Right);
                if (Y == MovementBounds.Top || Y == MovementBounds.Bottom)
                {
                    ForwardVelocity.Velocity = 0;
                }
                if (X == MovementBounds.Left || X == MovementBounds.Right)
                {
                    HorizontalVelocity.Velocity = 0;
                }
            }
        }
    }

    public class VelocityTracker
    {
        public double MaximumVelocity { get; set; }
        public double MinimumVelocity { get; set; }
        public double Acceleration { get; set; }
        public double Velocity { get; set; }

        public virtual void ApplyForce(double force)
        {
            Acceleration = MathUtils.Clamp(
                MinimumVelocity,
                force,
                MaximumVelocity);
        }

        public virtual void Update(TimeSpan deltaTime)
        {
            var secondsElapsed = deltaTime.TotalSeconds;

            Velocity = MathUtils.Clamp(
                MinimumVelocity,
                Velocity + (Acceleration * secondsElapsed),
                MaximumVelocity);
        }
    }

    public class MathUtils
    {
        public static double Clamp(double min, double value, double max)
        {
            return Math.Min(Math.Max(min, value), max);
        }
    }
}
