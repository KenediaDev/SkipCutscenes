using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD.Controls.Intern;
using Point = Microsoft.Xna.Framework.Point;
using SysMouse = Microsoft.Xna.Framework.Input.Mouse;
using Mouse = Blish_HUD.Controls.Intern.Mouse;
using Keyboard = Blish_HUD.Controls.Intern.Keyboard;
using System.Collections.Generic;

namespace Kenedia.Modules.SkipCutscenes
{
    [Export(typeof(Module))]
    public class SkipCutscenes : Module
    {
        internal static SkipCutscenes ModuleInstance;
        public static readonly Logger Logger = Logger.GetLogger<SkipCutscenes>();

        #region Service Managers

        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        #endregion

        [ImportingConstructor]
        public SkipCutscenes([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            ModuleInstance = this;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int Width, int Height, bool Repaint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        public SettingEntry<bool> ShowCornerIcon;
        public SettingEntry<Blish_HUD.Input.KeyBinding> Cancel_Key;

        public string CultureString;
        public TextureManager TextureManager;
        public Ticks Ticks = new Ticks();

        public WindowBase2 MainWindow;
        private CornerIcon CornerIcon;

        private int MumbleTick;
        private Point Resolution;
        private Vector3 PPos = Vector3.Zero;
        private bool InGame;
        private bool ModuleActive;
        private bool ClickAgain;
        private bool SleptBeforeClick;
        private bool IntroCutscene;

        List<int> IntroMaps = new List<int>()
        {
            573, //Queensdale
            458, //Plains of Ashford
            138, //Wayfarer Foothills
            379, //Caledon Forest
            432 //Metrica Province
        };
        List<int> StarterMaps = new List<int>(){
            15, //Queensdale
            19, //Plains of Ashford
            28, //Wayfarer Foothills
            34, //Caledon Forest
            35 //Metrica Province
        };

        private bool _DataLoaded;
        public bool FetchingAPI;
        public bool DataLoaded
        {
            get => _DataLoaded;
            set
            {
                _DataLoaded = value;
                if (value) ModuleInstance.OnDataLoaded();
            }
        }

        public event EventHandler DataLoaded_Event;
        void OnDataLoaded()
        {
            this.DataLoaded_Event?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler LanguageChanged;
        public void OnLanguageChanged(object sender, EventArgs e)
        {
            this.LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            ShowCornerIcon = settings.DefineSetting(nameof(ShowCornerIcon),
                                                      true,
                                                      () => Strings.common.ShowCorner_Name,
                                                      () => string.Format(Strings.common.ShowCorner_Tooltip, Name));

            var internal_settings = settings.AddSubCollection("Internal Settings", false);
            Cancel_Key = internal_settings.DefineSetting(nameof(Cancel_Key), new Blish_HUD.Input.KeyBinding(Keys.Escape));
        }

        protected override void Initialize()
        {
            Logger.Info($"Starting  {Name} v." + Version.BaseVersion());

            Cancel_Key.Value.Enabled = true;
            Cancel_Key.Value.Activated += Value_Activated;

            DataLoaded = false;
        }

        private void Value_Activated(object sender, EventArgs e)
        {
            Ticks.global += 2500;
            ClickAgain = false;
            SleptBeforeClick = false;
            MumbleTick = GameService.Gw2Mumble.Tick + 5;
        }

        private void ToggleWindow_Activated(object sender, EventArgs e)
        {
            MainWindow?.ToggleWindow();
        }

        protected override async Task LoadAsync()
        {
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            TextureManager = new TextureManager();

            CornerIcon = new CornerIcon()
            {
                Icon = TextureManager.getIcon(_Icons.ModuleIcon),
                HoverIcon = TextureManager.getIcon(_Icons.ModuleIcon_HoveredWhite),
                BasicTooltipText = string.Format(Strings.common.Toggle, $"{Name}"),
                Parent = GameService.Graphics.SpriteScreen,
                Visible = true,
            };

            DataLoaded_Event += SkipCutscenes_DataLoaded_Event;
            CornerIcon.Click += ToggleModule;
            OverlayService.Overlay.UserLocale.SettingChanged += UserLocale_SettingChanged;

            GameService.Gw2Mumble.CurrentMap.MapChanged += CurrentMap_MapChanged;
            GameService.Gw2Mumble.PlayerCharacter.NameChanged += PlayerCharacter_NameChanged; ;

            // Base handler must be called
            base.OnModuleLoaded(e);

            LoadData();
        }

        private void PlayerCharacter_NameChanged(object sender, ValueEventArgs<string> e)
        {
            IntroCutscene = false;
        }

        private void CurrentMap_MapChanged(object sender, ValueEventArgs<int> e)
        {
            ClickAgain = false;
            SleptBeforeClick = false;
            Ticks.global += 2000;
            MumbleTick = GameService.Gw2Mumble.Tick;

            var p = GameService.Gw2Mumble.PlayerCharacter.Position;
            PPos = p;

            if (IntroCutscene && StarterMaps.Contains(GameService.Gw2Mumble.CurrentMap.Id))
            {
                Thread.Sleep(1250);
                Click();
            }
        }

        private void SkipCutscenes_DataLoaded_Event(object sender, EventArgs e)
        {
            CreateUI();
        }

        private void ToggleModule(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            ModuleActive = !ModuleActive;

            if (CornerIcon != null)
            {
                CornerIcon.Icon = ModuleActive ? TextureManager.getIcon(_Icons.ModuleIcon_Active) : TextureManager.getIcon(_Icons.ModuleIcon);
                CornerIcon.HoverIcon = ModuleActive ? TextureManager.getIcon(_Icons.ModuleIcon_Active_HoveredWhite) : TextureManager.getIcon(_Icons.ModuleIcon_HoveredWhite);
            }
        }

        private void ShowCornerIcon_SettingChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            if (CornerIcon != null) CornerIcon.Visible = e.NewValue;
        }
        void Click()
        {
            var mousePos = Mouse.GetPosition();
            mousePos = new System.Drawing.Point(mousePos.X, mousePos.Y);

            var pos = new RECT();
            GetWindowRect(GameService.GameIntegration.Gw2Instance.Gw2WindowHandle, ref pos);
            var p = new System.Drawing.Point(GameService.Graphics.Resolution.X + pos.Left - 35, GameService.Graphics.Resolution.Y + pos.Top);

            Mouse.SetPosition(p.X, p.Y, true);
            Thread.Sleep(25);

            Mouse.Click(MouseButton.LEFT, p.X, p.Y, true);

            Thread.Sleep(10);
            Mouse.SetPosition(mousePos.X, mousePos.Y, true);
        }

        protected override void Update(GameTime gameTime)
        {
            if (gameTime.TotalGameTime.TotalMilliseconds - Ticks.global > 5 && ModuleActive)
            {
                Ticks.global = gameTime.TotalGameTime.TotalMilliseconds;

                var Mumble = GameService.Gw2Mumble;
                var resolution = GameService.Graphics.Resolution;
                var _inGame = GameService.GameIntegration.Gw2Instance.IsInGame;

                if (IntroMaps.Contains(Mumble.CurrentMap.Id))
                {
                    IntroCutscene = true;
                }
                else if (IntroCutscene)
                {
                }

                if (GameService.Graphics.Resolution != resolution)
                {
                    Resolution = resolution;
                    MumbleTick = Mumble.Tick + 5;
                    return;
                }

                if (!_inGame && (InGame || ClickAgain) && GameService.GameIntegration.Gw2Instance.Gw2HasFocus)
                {
                    if (Mumble.Tick > MumbleTick)
                    {
                        //ScreenNotification.ShowNotification("Click ... ", ScreenNotification.NotificationType.Error);
                        Click();

                        ClickAgain = true;
                        MumbleTick = Mumble.Tick;
                        Ticks.global = gameTime.TotalGameTime.TotalMilliseconds + 250;
                    }
                    else if (ClickAgain)
                    {
                        if (!SleptBeforeClick)
                        {
                            //ScreenNotification.ShowNotification("Sleep before we click again... ", ScreenNotification.NotificationType.Error);
                            Ticks.global = gameTime.TotalGameTime.TotalMilliseconds + 3500;
                            SleptBeforeClick = true;
                            return;
                        }

                        //ScreenNotification.ShowNotification("Click Again... ", ScreenNotification.NotificationType.Error);
                        ClickAgain = false;

                        Click();

                        //Thread.Sleep(5);
                        Mouse.Click(MouseButton.LEFT, 15, 15);

                        Thread.Sleep(5);
                        Keyboard.Stroke(Blish_HUD.Controls.Extern.VirtualKeyShort.ESCAPE);
                    }
                }
                else
                {
                    ClickAgain = false;
                    SleptBeforeClick = false;
                }

                InGame = GameService.GameIntegration.Gw2Instance.IsInGame;
            }
        }

        protected void UpdateOG(GameTime gameTime)
        {
            Ticks.global += gameTime.ElapsedGameTime.TotalMilliseconds;

            if (Ticks.global > 25 && ModuleActive)
            {
                Ticks.global = 0;

                var Mumble = GameService.Gw2Mumble;

                var mouse = SysMouse.GetState();
                var mouseState = (mouse.LeftButton == ButtonState.Released) ? ButtonState.Released : ButtonState.Pressed;

                if (mouseState == ButtonState.Pressed || GameService.Graphics.Resolution != Resolution)
                {
                    Resolution = GameService.Graphics.Resolution;
                    MumbleTick = Mumble.Tick + 5;
                    return;
                }

                if (!GameService.GameIntegration.Gw2Instance.IsInGame && InGame && Mumble.Tick > MumbleTick)
                {
                    Blish_HUD.Controls.Intern.Keyboard.Stroke(Blish_HUD.Controls.Extern.VirtualKeyShort.ESCAPE, false);
                    Blish_HUD.Controls.Intern.Mouse.Click(Blish_HUD.Controls.Intern.MouseButton.LEFT, 5, 5);

                    MumbleTick = Mumble.Tick + 1;
                }
                InGame = GameService.GameIntegration.Gw2Instance.IsInGame;
            }
        }

        protected override void Unload()
        {
            MainWindow?.Dispose();
            CornerIcon?.Dispose();

            TextureManager?.Dispose();
            TextureManager = null;

            if (CornerIcon != null) CornerIcon.Click -= ToggleModule;

            Cancel_Key.Value.Activated -= Value_Activated;
            DataLoaded_Event -= SkipCutscenes_DataLoaded_Event;
            OverlayService.Overlay.UserLocale.SettingChanged -= UserLocale_SettingChanged;

            DataLoaded = false;
            ModuleInstance = null;
        }


        public async Task Fetch_APIData(bool force = false)
        {
        }

        async Task LoadData()
        {

        }

        private async void UserLocale_SettingChanged(object sender, ValueChangedEventArgs<Gw2Sharp.WebApi.Locale> e)
        {
            await LoadData();

            CornerIcon.BasicTooltipText = string.Format(Strings.common.Toggle, $"{Name}");

            OnLanguageChanged(null, null);
        }

        private void CreateUI()
        {

        }
    }
}