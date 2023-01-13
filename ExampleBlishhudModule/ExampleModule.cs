using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Overlay.UI.Views;
using Blish_HUD.Settings;
using Gw2Sharp.Mumble.Models;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace ExampleBlishhudModule
{
    [Export(typeof(Module))]
    public class ExampleModule : Module
    {
        private static readonly Logger Logger = Logger.GetLogger<ExampleModule>();

        #region Service Managers

        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        #endregion

        // Ideally you should keep the constructor as is.
        // Use <see cref="Initialize"/> to handle initializing the module.
        [ImportingConstructor]
        public ExampleModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            ExampleModuleInstance = this;
        }

        // Define the settings you would like to use in your module.  Settings are persistent
        // between updates to both Blish HUD and your module.
        protected override void DefineSettings(SettingCollection settings)
        {

            _enumExampleSetting = settings.DefineSetting("UI size",
                UiSize.Small,
                () => "UI Size",
                () => "...");
        }

        // Allows your module to perform any initialization it needs before starting to run.
        // Please note that Initialize is NOT asynchronous and will block Blish HUD's update
        // and render loop, so be sure to not do anything here that takes too long.
        protected override void Initialize()
        {

            _forgeDelimiterContainer = new MyContainer()
            {
                BackgroundColor  = Color.Transparent,
                HeightSizingMode = SizingMode.AutoSize,
                WidthSizingMode  = SizingMode.AutoSize,
                Location         = new Point(1000, 1228),
                Parent           = GameService.Graphics.SpriteScreen
            };

            _enterForgeLabel = new Label()
            {
                Text           = "|",
                TextColor      = Color.Black,
                Font           = GameService.Content.DefaultFont16,
                ShowShadow     = true,
                AutoSizeHeight = true,
                AutoSizeWidth  = false,
                Location       = new Point(3, 0),
                Parent         = _forgeDelimiterContainer
            };

            _exitForgeLabel = new Label()
            {
                Text           = "|", 
                TextColor      = Color.Black,
                Font           = GameService.Content.DefaultFont16,
                ShowShadow     = true,
                AutoSizeHeight = true,
                AutoSizeWidth  = false,
                Location       = new Point(100, 0),
                Parent         = _forgeDelimiterContainer
            };
        }

        // Some API requests need an api key. e.g. accessing account data like inventory or bank content
        // Blish hud gives you an api subToken you can use instead of the real api key the user entered in blish.
        // But this api subToken may not be available when your module is loaded.
        // Because of that api requests, which require an api key, may fail when they are called in Initialize() or LoadAsync().
        // Or the user can delete the api key or add a new api key with the wrong permissions while your module is already running.
        // You can react to that by subscribing to Gw2ApiManager.SubtokenUpdated. This event will be raised when your module gets the api subToken or
        // when the user adds a new API key.
        private async void OnApiSubTokenUpdated(object sender, ValueEventArgs<IEnumerable<TokenPermission>> e)
        {
            return;
        }
        
        // Load content and more here. This call is asynchronous, so it is a good time to run
        // any long running steps for your module including loading resources from file or ref.
        protected override async Task LoadAsync()
        {
            return;
        }

        // Allows you to perform an action once your module has finished loading (once
        // <see cref="LoadAsync"/> has completed).  You must call "base.OnModuleLoaded(e)" at the
        // end for the <see cref="ExampleModule.ModuleLoaded"/> event to fire.
        protected override void OnModuleLoaded(EventArgs e)
        {
            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        // Allows your module to run logic such as updating UI elements,
        // checking for conditions, playing audio, calculating changes, etc.
        // This method will block the primary Blish HUD loop, so any long
        // running tasks should be executed on a separate thread to prevent
        // slowing down the overlay.
        protected override void Update(GameTime gameTime)
        {
           
        }

        // For a good module experience, your module should clean up ANY and ALL entities
        // and controls that were created and added to either the World or SpriteScreen.
        // Be sure to remove any tabs added to the Director window, CornerIcons, etc.
        protected override void Unload()
        {
            _forgeDelimiterContainer?.Dispose(); // this will dispose the child labels we added as well

            // All static members must be manually unset
            // Static members are not automatically cleared and will keep a reference to your,
            // module unless manually unset.
            ExampleModuleInstance = null;
        }

        private async Task GetCharacterNamesFromApiAndShowThemInLabel()
        {
            // check if api subToken has the permissions you need for your request: Gw2ApiManager.HasPermissions() 
            // Make sure that you added the api key permissions you need in the manifest.json.
            // e.g. the api request further down in this code needs the "characters" permission.
            // You can get the api permissions inside the manifest.json with Gw2ApiManager.Permissions
            // if the Gw2ApiManager.HasPermissions returns false it can also mean, that your module did not get the api subtoken yet or the user removed
            // the api key from blish hud. Because of that it is best practice to call .HasPermissions before every api request which requires an api key
            // and not only rely on Gw2ApiManager.SubtokenUpdated 
            if (Gw2ApiManager.HasPermissions(Gw2ApiManager.Permissions) == false)
            {
                _exitForgeLabel.Text = "api permissions are missing or api sub token not available yet";
                return;
            }

            // even when the api request and api subToken are okay, the api requests can still fail for various reasons.
            // Examples are timeouts or the api is down or the api randomly responds with an error code instead of the correct response.
            // Because of that use try catch when doing api requests to catch api request exceptions.
            // otherwise api request exceptions can crash your module and blish hud.
            try
            {
                // request characters endpoint from api. 
                var charactersResponse = await Gw2ApiManager.Gw2ApiClient.V2.Characters.AllAsync();
                // extract character names from the api response and show them inside a label
                var characterNames = charactersResponse.Select(c => c.Name);
                var characterNamesText = string.Join("\n", characterNames);
                _exitForgeLabel.Text = characterNamesText;
            }
            catch (Exception e)
            {
                // this is just an example for logging.
                // You do not have to log api response exception. Just make sure that your module has no issue with failing api requests
                Logger.Info($"Failed to get character names from api.");
            }
        }

        internal static ExampleModule ExampleModuleInstance;
        private SettingEntry<UiSize> _enumExampleSetting;
        private SettingEntry<int> _hiddenIntExampleSetting;
        private SettingEntry<int> _hiddenIntExampleSetting2;
        private Label _enterForgeLabel;
        private Label _exitForgeLabel;
        private MyContainer _forgeDelimiterContainer;
        private StandardWindow _exampleWindow;
    }
   
}