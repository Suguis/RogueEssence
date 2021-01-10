﻿using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Dungeon;
using RogueEssence.Script;
using System.Xml;
using System.Collections.Generic;

namespace RogueEssence.Content
{
    //Responsible for managing all graphics and sound for the game
    public static class GraphicsManager
    {
        [Flags]
        public enum AssetType
        {
            None = 0,
            Font = 1,
            Chara = 2,
            Portrait = 4,
            Tile = 8,
            Item = 16,
            VFX = 32,
            Icon = 64,
            Object = 128,
            BG = 256,
            Autotile = 512,
            All = 1023,
            Count = 1024
        }

        public enum GameZoom
        {
            x8Near = -3,
            x4Near = -2,
            x2Near = -1,
            x1 = 0,
            x2Far = 1,
            x4Far = 2,
            x8Far = 3,
        }

        public static int ConvertZoom(this GameZoom zoom, int amount)
        {
            switch (zoom)
            {
                case GameZoom.x8Near:
                    return amount * 8;
                case GameZoom.x4Near:
                    return amount * 4;
                case GameZoom.x2Near:
                    return amount * 2;
                case GameZoom.x2Far:
                    return amount / 2;
                case GameZoom.x4Far:
                    return amount / 4;
                case GameZoom.x8Far:
                    return amount / 8;
                default:
                    return amount;
            }
        }

        public static float GetScale(this GameZoom zoom)
        {
            switch (zoom)
            {
                case GameZoom.x8Near:
                    return 8f;
                case GameZoom.x4Near:
                    return 4f;
                case GameZoom.x2Near:
                    return 2f;
                case GameZoom.x2Far:
                    return 0.5f;
                case GameZoom.x4Far:
                    return 0.25f;
                case GameZoom.x8Far:
                    return 0.125f;
                default:
                    return 1.0f;
            }
        }

        public const string BASE_PATH = PathMod.ASSET_PATH + "Base/";
        public const string FONT_PATTERN = PathMod.ASSET_PATH + "Font/{0}.font";

        public const string MUSIC_PATH = CONTENT_PATH + "Music/";
        public const string SOUND_PATH = CONTENT_PATH + "Sound/";
        public const string UI_PATH = CONTENT_PATH + "UI/";

        public const string CONTENT_PATH = "Content/";

        public const string CHARA_PATTERN = CONTENT_PATH + "Chara/{0}.chara";
        public const string PORTRAIT_PATTERN = CONTENT_PATH + "Portrait/{0}.portrait";
        public const string PARTICLE_PATTERN = CONTENT_PATH + "Particle/{0}.dir";
        public const string ITEM_PATTERN = CONTENT_PATH + "Item/{0}.dir";
        public const string BEAM_PATTERN = CONTENT_PATH + "Beam/{0}.beam";
        public const string ICON_PATTERN = CONTENT_PATH + "Icon/{0}.dir";
        public const string TILE_PATTERN = CONTENT_PATH + "Tile/{0}.tile";
        public const string OBJECT_PATTERN = CONTENT_PATH + "Object/{0}.dir";
        public const string BG_PATTERN = CONTENT_PATH + "BG/{0}.dir";


        public const int TEX_SIZE = 8;

        public static int DungeonTexSize;
        public static int TileSize { get { return DungeonTexSize * TEX_SIZE; } }
        public static int ScreenWidth;
        public static int ScreenHeight;
        public static int PortraitSize;
        public static List<string> Emotions;
        public static int SOSEmotion;
        public static int AOKEmotion;
        public static List<CharFrameType> Actions;
        public static int HurtAction;
        public static int WalkAction;
        public static int IdleAction;
        public static int SleepAction;
        public static int ChargeAction;

        public const int MAX_FPS = 60;

        private const int CHARA_CACHE_SIZE = 200;
        private const int PORTRAIT_CACHE_SIZE = 50;
        private const int VFX_CACHE_SIZE = 100;
        private const int ICON_CACHE_SIZE = 100;
        private const int ITEM_CACHE_SIZE = 100;
        private const int TILE_CACHE_SIZE_PIXELS = 5000000;
        private const int OBJECT_CACHE_SIZE = 500;
        private const int BG_CACHE_SIZE = 10;

        public static ulong TotalFrameTick;

        private static int windowZoom;
        public static int WindowZoom
        {
            get { return windowZoom; }
            set
            {
                windowZoom = value;
                graphics.PreferredBackBufferWidth = WindowWidth;
                graphics.PreferredBackBufferHeight = WindowHeight;
                graphics.ApplyChanges();
            }
        }
        public static bool FullScreen
        {
            get { return graphics.IsFullScreen; }
            set
            {
                graphics.IsFullScreen = value;
                graphics.ApplyChanges();
            }
        }

        public static int WindowWidth { get { return windowZoom * ScreenWidth; } }
        public static int WindowHeight { get { return windowZoom * ScreenHeight; } }

        private static GraphicsDeviceManager graphics;
        public static GraphicsDevice GraphicsDevice { get { return graphics.GraphicsDevice; } }

        public static int GlobalIdle;

        private static Texture2D defaultTex;
        public static BaseSheet Pixel { get; private set; }

        private static LRUCache<MonsterID, CharSheet> spriteCache;
        private static LRUCache<MonsterID, PortraitSheet> portraitCache;
        private static LRUCache<string, IEffectAnim> vfxCache;
        private static LRUCache<string, DirSheet> iconCache;
        private static LRUCache<string, DirSheet> itemCache;
        private static LRUCache<string, DirSheet> objectCache;
        private static LRUCache<TileFrame, BaseSheet> tileCache;
        private static LRUCache<string, DirSheet> bgCache;

        public static FontSheet TextFont { get; private set; }
        public static FontSheet DungeonFont { get; private set; }
        public static FontSheet DamageFont { get; private set; }
        public static FontSheet HealFont { get; private set; }
        public static FontSheet EXPFont { get; private set; }
        public static FontSheet SysFont { get; private set; }

        public static TileGuide TileIndex { get; private set; }
        public static CharaIndexNode CharaIndex { get; private set; }
        public static CharaIndexNode PortraitIndex { get; private set; }

        public static BaseSheet DivTex { get; private set; }
        public static TileSheet MenuBG { get; private set; }
        public static TileSheet MenuBorder { get; private set; }
        public static TileSheet PicBorder { get; private set; }
        public static TileSheet Arrows { get; private set; }
        public static TileSheet Cursor { get; private set; }
        public static TileSheet BattleFactors { get; private set; }
        public static TileSheet Shadows { get; private set; }
        public static TileSheet Tiling { get; private set; }
        public static TileSheet Darkness { get; private set; }
        public static TileSheet Strip { get; private set; }
        public static TileSheet Buttons { get; private set; }
        public static TileSheet HPMenu { get; private set; }
        public static BaseSheet MiniHP { get; private set; }
        public static TileSheet MapSheet { get; private set; }
        public static BaseSheet Splash { get; private set; }


        public static BaseSheet Title { get; private set; }
        public static BaseSheet Subtitle { get; private set; }


        public static string HungerSE { get; private set; }
        public static string NullDmgSE { get; private set; }
        public static string CursedSE { get; private set; }
        public static string PickupSE { get; private set; }
        public static string PickupFoeSE { get; private set; }
        public static string ReplaceSE { get; private set; }
        public static string PlaceSE { get; private set; }
        public static string EquipSE { get; private set; }
        public static string MoneySE { get; private set; }
        public static string LeaderSE { get; private set; }
        public static string ReviveSE { get; private set; }

        public static string TitleBG { get; private set; }

        public static string TitleBGM { get; private set; }
        public static string MonsterBGM { get; private set; }

        public static bool Loaded;

        public static void InitParams()
        {
            string path = BASE_PATH + "GFXParams.xml";
            //try to load from file
            if (File.Exists(path))
            {
                try
                {
                    XmlDocument xmldoc = new XmlDocument();
                    xmldoc.Load(path);

                    XmlNode tileSize = xmldoc.DocumentElement.SelectSingleNode("TileSize");
                    DungeonTexSize = Int32.Parse(tileSize.InnerText) / TEX_SIZE;

                    XmlNode portraitSize = xmldoc.DocumentElement.SelectSingleNode("PortraitSize");
                    PortraitSize = Int32.Parse(portraitSize.InnerText);

                    XmlNode screenWidth = xmldoc.DocumentElement.SelectSingleNode("ScreenWidth");
                    ScreenWidth = Int32.Parse(screenWidth.InnerText);

                    XmlNode screenHeight = xmldoc.DocumentElement.SelectSingleNode("ScreenHeight");
                    ScreenHeight = Int32.Parse(screenHeight.InnerText);

                    Emotions = new List<string>();
                    XmlNode emotions = xmldoc.DocumentElement.SelectSingleNode("Emotions");
                    foreach (XmlNode emotion in emotions.SelectNodes("Emotion"))
                        Emotions.Add(emotion.InnerText);

                    XmlNode sosEmotion = xmldoc.DocumentElement.SelectSingleNode("SOSEmotion");
                    SOSEmotion = Int32.Parse(sosEmotion.InnerText);

                    XmlNode aokEmotion = xmldoc.DocumentElement.SelectSingleNode("AOKEmotion");
                    AOKEmotion = Int32.Parse(aokEmotion.InnerText);

                    Actions = new List<CharFrameType>();
                    XmlNode actions = xmldoc.DocumentElement.SelectSingleNode("Actions");
                    foreach (XmlNode action in actions.SelectNodes("Action"))
                    {
                        XmlNode actionName = action.SelectSingleNode("Name");
                        XmlNode actionDash = action.SelectSingleNode("Dash");
                        Actions.Add(new CharFrameType(actionName.InnerText, Boolean.Parse(actionDash.InnerText)));
                    }

                    XmlNode hurtAction = xmldoc.DocumentElement.SelectSingleNode("HurtAction");
                    HurtAction = Int32.Parse(hurtAction.InnerText);

                    XmlNode walkAction = xmldoc.DocumentElement.SelectSingleNode("WalkAction");
                    WalkAction = Int32.Parse(walkAction.InnerText);

                    XmlNode idleAction = xmldoc.DocumentElement.SelectSingleNode("IdleAction");
                    IdleAction = Int32.Parse(idleAction.InnerText);

                    XmlNode sleepAction = xmldoc.DocumentElement.SelectSingleNode("SleepAction");
                    SleepAction = Int32.Parse(sleepAction.InnerText);

                    XmlNode chargeAction = xmldoc.DocumentElement.SelectSingleNode("ChargeAction");
                    ChargeAction = Int32.Parse(chargeAction.InnerText);

                    return;
                }
                catch (Exception ex)
                {
                    DiagManager.Instance.LogError(ex);
                }
            }
        }


        public static void InitBase(GraphicsDeviceManager newGraphics, int zoom, bool fullScreen)
        {
            graphics = newGraphics;
            WindowZoom = zoom;
            FullScreen = fullScreen;
        }

        public static void InitSystem(GraphicsDevice graphics)
        {
            defaultTex = new Texture2D(graphics, 32, 32);
            for (int ii = 0; ii < 16; ii++)
                BaseSheet.BlitColor((ii % 2 == (ii / 4 % 2)) ? Color.Black : new Color(255, 0, 255, 255), defaultTex, 8, 8, ii % 4 * 8, ii / 4 * 8);
            //Set graphics device
            BaseSheet.InitBase(graphics, defaultTex);

            Splash = BaseSheet.Import(BASE_PATH + "Splash.png");

            SysFont = LoadFont("system");
        }

        public static void InitStatic()
        {
            DiagManager.Instance.LoadMsg = "Loading Graphics";

            //load onepixel
            Pixel = new BaseSheet(1, 1);
            Pixel.BlitColor(Color.White, 1, 1, 0, 0);

            //Load divider texture
            DivTex = BaseSheet.Import(BASE_PATH + "Divider.png");

            //load menu data
            MenuBG = TileSheet.Import(BASE_PATH + "MenuBG.png", 8, 8);

            //load menu data
            MenuBorder = TileSheet.Import(BASE_PATH + "MenuBorder.png", 8, 8);

            //load menu data
            PicBorder = TileSheet.Import(BASE_PATH + "PortraitBorder.png", 4, 4);

            Arrows = TileSheet.Import(BASE_PATH + "Arrows.png", 8, 8);

            Cursor = TileSheet.Import(BASE_PATH + "Cursor.png", 11, 11);

            BattleFactors = TileSheet.Import(BASE_PATH + "BattleFactors.png", 16, 16);

            Tiling = TileSheet.Import(BASE_PATH + "Tiling.png", TileSize, TileSize);

            Darkness = TileSheet.Import(BASE_PATH + "Dark.png", 8, 8);

            Strip = TileSheet.Import(BASE_PATH + "Strip.png", 8, 8);

            Buttons = TileSheet.Import(BASE_PATH + "Buttons.png", 16, 16);

            HPMenu = TileSheet.Import(BASE_PATH + "HP.png", 8, 8);

            MiniHP = BaseSheet.Import(BASE_PATH + "MiniHP.png");

            MapSheet = TileSheet.Import(BASE_PATH + "Map.png", 4, 4);

            Shadows = TileSheet.Import(BASE_PATH + "Shadows.png", 32, 16);

            Title = BaseSheet.Import(PathMod.ModPath(UI_PATH + "Title.png"));
            Subtitle = BaseSheet.Import(PathMod.ModPath(UI_PATH + "Enter.png"));

            LoadContentParams();

            //load font
            TextFont = LoadFont("text");
            DungeonFont = LoadFont("banner");
            DamageFont = LoadFont("yellow");
            HealFont = LoadFont("green");
            EXPFont = LoadFont("blue");

            Menu.MenuBase.BorderStyle = DiagManager.Instance.CurSettings.Border;

            DiagManager.Instance.LoadMsg = "Loading Headers";

            //initialize caches
            spriteCache = new LRUCache<MonsterID, CharSheet>(CHARA_CACHE_SIZE);
            spriteCache.OnItemRemoved = DisposeCachedObject;
            portraitCache = new LRUCache<MonsterID, PortraitSheet>(PORTRAIT_CACHE_SIZE);
            portraitCache.OnItemRemoved = DisposeCachedObject;
            vfxCache = new LRUCache<string, IEffectAnim>(VFX_CACHE_SIZE);
            vfxCache.OnItemRemoved = DisposeCachedObject;
            iconCache = new LRUCache<string, DirSheet>(ICON_CACHE_SIZE);
            iconCache.OnItemRemoved = DisposeCachedObject;
            itemCache = new LRUCache<string, DirSheet>(ITEM_CACHE_SIZE);
            itemCache.OnItemRemoved = DisposeCachedObject;
            tileCache = new LRUCache<TileFrame, BaseSheet>(TILE_CACHE_SIZE_PIXELS);
            tileCache.ItemCount = CountPixels;
            tileCache.OnItemRemoved = DisposeCachedObject;
            bgCache = new LRUCache<string, DirSheet>(BG_CACHE_SIZE);
            bgCache.OnItemRemoved = DisposeCachedObject;
            objectCache = new LRUCache<string, DirSheet>(OBJECT_CACHE_SIZE);
            objectCache.OnItemRemoved = DisposeCachedObject;

            //load guides
            CharaIndex = LoadCharaIndices(CONTENT_PATH + "Chara/");
            PortraitIndex = LoadCharaIndices(CONTENT_PATH + "Portrait/");
            TileIndex = LoadTileIndices(CONTENT_PATH + "Tile/");

            Loaded = true;
            //Notify script engine
            LuaEngine.Instance.OnGraphicsLoad();
        }

        public static void ReloadStatic()
        {
            //maybe add other graphics here later on
            Title = BaseSheet.Import(PathMod.ModPath(UI_PATH + "Title.png"));
            Subtitle = BaseSheet.Import(PathMod.ModPath(UI_PATH + "Enter.png"));

            LoadContentParams();

            ClearCaches(AssetType.All);
        }

        public static void Unload()
        {
            Subtitle.Dispose();
            Title.Dispose();
            Splash.Dispose();
            MapSheet.Dispose();
            MiniHP.Dispose();
            HPMenu.Dispose();
            Buttons.Dispose();
            Shadows.Dispose();
            Darkness.Dispose();
            BattleFactors.Dispose();
            Strip.Dispose();
            Cursor.Dispose();
            Arrows.Dispose();
            PicBorder.Dispose();
            MenuBorder.Dispose();
            MenuBG.Dispose();

            tileCache.Clear();
            objectCache.Clear();
            bgCache.Clear();
            itemCache.Clear();
            iconCache.Clear();
            vfxCache.Clear();
            portraitCache.Clear();
            spriteCache.Clear();

            DivTex.Dispose();

            EXPFont.Dispose();
            HealFont.Dispose();
            DamageFont.Dispose();
            DungeonFont.Dispose();
            TextFont.Dispose();

            SysFont.Dispose();

            Pixel.Dispose();
            defaultTex.Dispose();

            Loaded = false;
            //Notify script engine
            LuaEngine.Instance.OnGraphicsUnload();
        }

        /// <summary>
        /// Bakes all assets from the "Work files" directory specified in the flags.
        /// </summary>
        /// <param name="conversionFlags">Chooses which asset type to bake</param>
        public static void RunConversions(AssetType conversionFlags)
        {
            if ((conversionFlags & AssetType.Font) != AssetType.None)
                Dev.ImportHelper.ImportAllFonts(PathMod.DEV_PATH+"Font/", FONT_PATTERN);
            if ((conversionFlags & AssetType.Chara) != AssetType.None)
            {
                Dev.ImportHelper.ImportAllChars(PathMod.DEV_PATH + "Sprite/", PathMod.HardMod(CHARA_PATTERN));
                Dev.ImportHelper.BuildCharIndex(CHARA_PATTERN);
            }
            if ((conversionFlags & AssetType.Portrait) != AssetType.None)
            {
                Dev.ImportHelper.ImportAllPortraits(PathMod.DEV_PATH + "Portrait/", PathMod.HardMod(PORTRAIT_PATTERN));
                Dev.ImportHelper.BuildCharIndex(PORTRAIT_PATTERN);
            }
            if ((conversionFlags & AssetType.Tile) != AssetType.None)
                Dev.ImportHelper.ImportAllTiles(PathMod.DEV_PATH+"Tiles/", PathMod.HardMod(TILE_PATTERN), true, false);

            if ((conversionFlags & AssetType.Item) != AssetType.None)
                Dev.ImportHelper.ImportAllItems(PathMod.DEV_PATH+"Item/", PathMod.HardMod(ITEM_PATTERN));
            if ((conversionFlags & AssetType.VFX) != AssetType.None)
                Dev.ImportHelper.ImportAllVFX(PathMod.DEV_PATH+"Attacks/", PathMod.HardMod(PARTICLE_PATTERN), PathMod.HardMod(BEAM_PATTERN));
            if ((conversionFlags & AssetType.Icon) != AssetType.None)
                Dev.ImportHelper.ImportAllDirs(PathMod.DEV_PATH+"Icon/", PathMod.HardMod(ICON_PATTERN));
            if ((conversionFlags & AssetType.Object) != AssetType.None)
                Dev.ImportHelper.ImportAllNameDirs(PathMod.DEV_PATH+"Object/", PathMod.HardMod(OBJECT_PATTERN));
            if ((conversionFlags & AssetType.BG) != AssetType.None)
                Dev.ImportHelper.ImportAllNameDirs(PathMod.DEV_PATH+"BG/", PathMod.HardMod(BG_PATTERN));

            if ((conversionFlags & AssetType.Autotile) != AssetType.None)
                // Old format (image data):
                Dev.ImportHelper.ImportAllTiles(PathMod.DEV_PATH + "Tiles/", PathMod.HardMod(TILE_PATTERN), false, true);

            if ((conversionFlags & AssetType.Autotile) != AssetType.None)
            {
                // Old format (auto tiles):
                Dev.ImportHelper.ImportAllAutoTiles(PathMod.DEV_PATH + "Tiles/", DataManager.DATA_PATH + "AutoTile/");
                // New format (image data & auto tiles):
                Dev.DtefImportHelper.ImportAllDtefTiles(PathMod.DEV_PATH + "TilesDtef/", PathMod.HardMod(TILE_PATTERN));
                
                Dev.DevHelper.IndexNamedData(DataManager.DATA_PATH + "AutoTile/");
            }
            
            if ((conversionFlags & AssetType.Tile) != AssetType.None || (conversionFlags & AssetType.Autotile) != AssetType.None)
                Dev.ImportHelper.BuildTileIndex(TILE_PATTERN);
        }

        public static void InitContentFolders(string baseFolder)
        {
            Directory.CreateDirectory(Path.Join(baseFolder, CONTENT_PATH));

            Directory.CreateDirectory(Path.Join(baseFolder, Path.GetDirectoryName(CHARA_PATTERN)));
            Directory.CreateDirectory(Path.Join(baseFolder, Path.GetDirectoryName(PORTRAIT_PATTERN)));
            Directory.CreateDirectory(Path.Join(baseFolder, Path.GetDirectoryName(PARTICLE_PATTERN)));
            Directory.CreateDirectory(Path.Join(baseFolder, Path.GetDirectoryName(ITEM_PATTERN)));
            Directory.CreateDirectory(Path.Join(baseFolder, Path.GetDirectoryName(BEAM_PATTERN)));
            Directory.CreateDirectory(Path.Join(baseFolder, Path.GetDirectoryName(ICON_PATTERN)));
            Directory.CreateDirectory(Path.Join(baseFolder, Path.GetDirectoryName(TILE_PATTERN)));
            Directory.CreateDirectory(Path.Join(baseFolder, Path.GetDirectoryName(OBJECT_PATTERN)));
            Directory.CreateDirectory(Path.Join(baseFolder, Path.GetDirectoryName(BG_PATTERN)));

            Directory.CreateDirectory(Path.Join(baseFolder, MUSIC_PATH));
            Directory.CreateDirectory(Path.Join(baseFolder, SOUND_PATH));
            Directory.CreateDirectory(Path.Join(baseFolder, UI_PATH));
        }

        public static void LoadContentParams()
        {
            string path = PathMod.ModPath(CONTENT_PATH + "ContentParams.xml");
            //try to load from file
            if (File.Exists(path))
            {
                try
                {

                    XmlDocument xmldoc = new XmlDocument();
                    xmldoc.Load(path);

                    XmlNode sysSounds = xmldoc.DocumentElement.SelectSingleNode("Sounds");

                    HungerSE = sysSounds.SelectSingleNode("Hunger").InnerText;
                    NullDmgSE = sysSounds.SelectSingleNode("NullDmg").InnerText;
                    CursedSE = sysSounds.SelectSingleNode("Cursed").InnerText;
                    PickupSE = sysSounds.SelectSingleNode("Pickup").InnerText;
                    PickupFoeSE = sysSounds.SelectSingleNode("PickupFoe").InnerText;
                    ReplaceSE = sysSounds.SelectSingleNode("Replace").InnerText;
                    PlaceSE = sysSounds.SelectSingleNode("Place").InnerText;
                    EquipSE = sysSounds.SelectSingleNode("Equip").InnerText;
                    MoneySE = sysSounds.SelectSingleNode("Money").InnerText;
                    LeaderSE = sysSounds.SelectSingleNode("Leader").InnerText;
                    ReviveSE = sysSounds.SelectSingleNode("Revive").InnerText;

                    TitleBG = xmldoc.DocumentElement.SelectSingleNode("TitleBG").InnerText;

                    TitleBGM = xmldoc.DocumentElement.SelectSingleNode("TitleBGM").InnerText;
                    MonsterBGM = xmldoc.DocumentElement.SelectSingleNode("MonsterBGM").InnerText;

                    return;
                }
                catch (Exception ex)
                {
                    DiagManager.Instance.LogError(ex);
                }
            }
        }

        public static void RebuildIndices(AssetType assetType)
        {
            if ((assetType & AssetType.Tile) != AssetType.None)
                Dev.ImportHelper.BuildTileIndex(TILE_PATTERN);

            if ((assetType & AssetType.Chara) != AssetType.None)
                Dev.ImportHelper.BuildCharIndex(CHARA_PATTERN);

            if ((assetType & AssetType.Portrait) != AssetType.None)
                Dev.ImportHelper.BuildCharIndex(PORTRAIT_PATTERN);
        }

        public static void ClearCaches(AssetType assetType)
        {
            if ((assetType & AssetType.Tile) != AssetType.None)
            {
                TileIndex = LoadTileIndices(CONTENT_PATH + "Tile/");
                tileCache.Clear();
                DiagManager.Instance.LogInfo("Tilesets Reloaded.");
            }

            if ((assetType & AssetType.BG) != AssetType.None)
            {
                bgCache.Clear();
                DiagManager.Instance.LogInfo("Backgrounds Reloaded.");
            }

            if ((assetType & AssetType.Chara) != AssetType.None)
            {
                CharaIndex = LoadCharaIndices(CONTENT_PATH + "Chara/");
                spriteCache.Clear();
                DiagManager.Instance.LogInfo("Characters Reloaded.");
            }

            if ((assetType & AssetType.Portrait) != AssetType.None)
            {
                PortraitIndex = LoadCharaIndices(CONTENT_PATH + "Portrait/");
                portraitCache.Clear();
                DiagManager.Instance.LogInfo("Portraits Reloaded.");
            }

            if ((assetType & AssetType.VFX) != AssetType.None)
            {
                vfxCache.Clear();
                DiagManager.Instance.LogInfo("Effects Reloaded.");
            }

            if ((assetType & AssetType.Item) != AssetType.None)
            {
                itemCache.Clear();
                DiagManager.Instance.LogInfo("Items Reloaded.");
            }

            if ((assetType & AssetType.Icon) != AssetType.None)
            {
                iconCache.Clear();
                DiagManager.Instance.LogInfo("Icons Reloaded.");
            }

            if ((assetType & AssetType.Object) != AssetType.None)
            {
                objectCache.Clear();
                DiagManager.Instance.LogInfo("Objects Reloaded.");
            }
        }

        private static FontSheet LoadFont(string prefix)
        {
            using (FileStream fileStream = File.OpenRead(String.Format(FONT_PATTERN, prefix)))
            {
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    FontSheet font = FontSheet.Load(reader);
                    return font;
                }
            }
        }

        private static CharaIndexNode LoadCharaIndices(string charaDir)
        {
            CharaIndexNode fullGuide = null;
            try
            {
                using (FileStream stream = File.OpenRead(PathMod.ModPath(charaDir + "index.idx")))
                {
                    using (BinaryReader reader = new BinaryReader(stream))
                        fullGuide = CharaIndexNode.Load(reader);
                }
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogError(new Exception("Error reading header at " + charaDir + "\n", ex));
            }
            return fullGuide;
        }

        public static MonsterID GetFallbackForm(CharaIndexNode guide, MonsterID data)
        {
            MonsterID fallback = MonsterID.Invalid;
            MonsterID buffer = MonsterID.Invalid;
            if (guide.Nodes.ContainsKey(data.Species))
            {
                buffer.Species = data.Species;
                if (guide.Nodes[data.Species].Position > 0)
                    fallback = buffer;
                if (guide.Nodes[data.Species].Nodes.ContainsKey(data.Form))
                {
                    buffer.Form = data.Form;
                    if (guide.Nodes[data.Species].Nodes[data.Form].Position > 0)
                        fallback = buffer;
                    int trialSkin = data.Skin;
                    while (trialSkin > -1)
                    {
                        if (guide.Nodes[data.Species].Nodes[data.Form].Nodes.ContainsKey(trialSkin))
                        {
                            buffer.Skin = trialSkin;
                            if (guide.Nodes[data.Species].Nodes[data.Form].Nodes[trialSkin].Position > 0)
                                fallback = buffer;
                            if (guide.Nodes[data.Species].Nodes[data.Form].Nodes[trialSkin].Nodes.ContainsKey((int)data.Gender))
                            {
                                buffer.Gender = data.Gender;
                                if (guide.Nodes[data.Species].Nodes[data.Form].Nodes[trialSkin].Nodes[(int)data.Gender].Position > 0)
                                    fallback = buffer;
                            }
                            break;
                        }
                        trialSkin--;
                    }
                }
            }
            return fallback;
        }

        public static CharSheet GetChara(MonsterID data)
        {
            data = GetFallbackForm(CharaIndex, data);

            CharSheet sheet;
            if (spriteCache.TryGetValue(data, out sheet))
                return sheet;

            if (data.IsValid())
            {
                try
                {
                    using (FileStream stream = File.OpenRead(PathMod.ModPath(String.Format(CHARA_PATTERN, data.Species))))
                    {
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            long position = CharaIndex.GetPosition(data.Species, data.Form, (int)data.Skin, (int)data.Gender);
                            // Jump to the correct position
                            stream.Seek(position, SeekOrigin.Begin);
                            sheet = CharSheet.Load(reader);
                            spriteCache.Add(data, sheet);
                            return sheet;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DiagManager.Instance.LogError(new Exception("Error loading chara " + data.Species + " " + data.Form + "-" + data.Skin + "-" + data.Gender + "\n", ex));
                }
            }

            //add error sheet
            CharSheet error = CharSheet.LoadError();
            spriteCache.Add(data, error);
            return error;
        }



        public static PortraitSheet GetPortrait(MonsterID data)
        {
            data = GetFallbackForm(PortraitIndex, data);

            PortraitSheet sheet;
            if (portraitCache.TryGetValue(data, out sheet))
                return sheet;

            if (data.IsValid())
            {
                try
                {
                    using (FileStream stream = File.OpenRead(PathMod.ModPath(String.Format(PORTRAIT_PATTERN, data.Species))))
                    {
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            long position = PortraitIndex.GetPosition(data.Species, data.Form, (int)data.Skin, (int)data.Gender);
                            // Jump to the correct position
                            stream.Seek(position, SeekOrigin.Begin);
                            sheet = PortraitSheet.Load(reader);
                            portraitCache.Add(data, sheet);
                            return sheet;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DiagManager.Instance.LogError(new Exception("Error loading portrait " + data.Species + " " + data.Form + "-" + data.Skin + "-" + data.Gender + "\n", ex));
                }
            }

            //add error sheet
            PortraitSheet error = PortraitSheet.LoadError();
            portraitCache.Add(data, error);
            return error;
        }

        public static BeamSheet GetBeam(string num)
        {
            IEffectAnim sheet;
            if (vfxCache.TryGetValue("Beam-" + num, out sheet))
                return (BeamSheet)sheet;

            string beamPath = PathMod.ModPath(String.Format(BEAM_PATTERN, num));
            try
            {
                if (File.Exists(beamPath))
                {
                    using (FileStream stream = File.OpenRead(beamPath))
                    {
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            sheet = BeamSheet.Load(reader);
                            vfxCache.Add("Beam-" + num, sheet);
                            return (BeamSheet)sheet;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogError(new Exception("Error loading " + beamPath + "\n", ex));
            }

            BeamSheet newSheet = BeamSheet.LoadError();
            vfxCache.Add("Beam-" + num, newSheet);
            return newSheet;
        }

        public static DirSheet GetAttackSheet(string name)
        {
            IEffectAnim sheet;
            if (vfxCache.TryGetValue("Particle-" + name, out sheet))
                return (DirSheet)sheet;

            string particlePath = PathMod.ModPath(String.Format(PARTICLE_PATTERN, name));
            try
            {
                if (File.Exists(particlePath))
                {
                    //read file and read binary data
                    using (FileStream fileStream = File.OpenRead(particlePath))
                    {
                        using (BinaryReader reader = new BinaryReader(fileStream))
                        {
                            sheet = DirSheet.Load(reader);
                            vfxCache.Add("Particle-" + name, sheet);
                            return (DirSheet)sheet;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogError(new Exception("Error loading " + particlePath + "\n", ex));
            }
            DirSheet newSheet = DirSheet.LoadError();
            vfxCache.Add("Particle-" + name, newSheet);
            return newSheet;
        }

        public static DirSheet GetIcon(int num)
        {
            return getDirSheetCache(num.ToString(), ICON_PATTERN, iconCache);
        }

        public static DirSheet GetItem(int num)
        {
            return getDirSheetCache(num.ToString(), ITEM_PATTERN, itemCache);
        }

        public static DirSheet GetBackground(string num)
        {
            return getDirSheetCache(num, BG_PATTERN, bgCache);
        }

        public static DirSheet GetObject(string num)
        {
            return getDirSheetCache(num, OBJECT_PATTERN, objectCache);
        }

        private static DirSheet getDirSheetCache(string num, string pattern, LRUCache<string, DirSheet> cache)
        {
            DirSheet sheet;
            if (cache.TryGetValue(num, out sheet))
                return sheet;

            string dirPath = PathMod.ModPath(String.Format(pattern, num));
            try
            {
                if (File.Exists(dirPath))
                {
                    //read file and read binary data
                    using (FileStream stream = File.OpenRead(dirPath))
                    {
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            sheet = DirSheet.Load(reader);
                            cache.Add(num, sheet);
                            return sheet;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogError(new Exception("Error loading " + dirPath + "\n", ex));
            }
            DirSheet newSheet = DirSheet.LoadError();
            cache.Add(num, newSheet);
            return newSheet;
        }


        private static TileGuide LoadTileIndices(string tileDir)
        {
            TileGuide fullGuide = null;
            try
            {
                using (FileStream stream = File.OpenRead(PathMod.ModPath(tileDir + "index.idx")))
                {
                    using (BinaryReader reader = new BinaryReader(stream))
                        fullGuide = TileGuide.Load(reader);
                }

            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogError(new Exception("Error reading header at " + tileDir + "\n", ex));
            }
            return fullGuide;
        }

        public static BaseSheet GetTile(TileFrame tileTex)
        {
            BaseSheet sheet;
            if (tileCache.TryGetValue(tileTex, out sheet))
                return sheet;

            long tilePos = TileIndex.GetPosition(tileTex.Sheet, tileTex.TexLoc);

            if (tilePos > 0)
            {
                try
                {
                    using (FileStream stream = new FileStream(PathMod.ModPath(String.Format(TILE_PATTERN, tileTex.Sheet)), FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            // Seek to the location of the tile
                            reader.BaseStream.Seek(tilePos, SeekOrigin.Begin);

                            sheet = BaseSheet.Load(reader);
                            tileCache.Add(tileTex, sheet);
                            return sheet;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DiagManager.Instance.LogError(new Exception("Error retrieving tile " + tileTex.TexLoc.X + ", " + tileTex.TexLoc.Y + " from Tileset #" + tileTex.Sheet + "\n", ex));
                }
            }

            BaseSheet newSheet = BaseSheet.LoadError();
            tileCache.Add(tileTex, newSheet);
            return newSheet;
        }

        public static int CountPixels(BaseSheet obj)
        {
            return obj.Width * obj.Height;
        }

        public static void DisposeCachedObject(IDisposable obj)
        {
            obj.Dispose();
        }

    }

}
