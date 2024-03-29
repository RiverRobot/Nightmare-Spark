﻿global using GlobalEnums;
global using HutongGames.PlayMaker;
global using HutongGames.PlayMaker.Actions;
global using ItemChanger;
global using ItemChanger.Locations;
global using ItemChanger.Tags;
global using ItemChanger.UIDefs;
global using Modding;
global using MonoMod.RuntimeDetour;
global using Satchel;
global using Satchel.Futils;
global using SFCore;
global using SFCore.Generics;
global using HKMirror;
global using HKMirror.Reflection;
global using HKMirror.Reflection.SingletonClasses;
global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.Reflection;
global using UnityEngine;


namespace Nightmare_Spark
{

    public class Nightmare_Spark : SaveSettingsMod<SaveSettings>
    {
        public static Nightmare_Spark Instance;

        new public string GetName() => "NightmareSpark";
        public override string GetVersion() => "V1.2";
        public Nightmare_Spark() : base("Nightmare Spark")
        {
            Ts = new TextureStrings();

        }
        public override List<(string, string)> GetPreloadNames()
        {
            return new List<(string, string)>()
            {
                ("GG_Grimm_Nightmare", "Grimm Control/Nightmare Grimm Boss"),
                ("GG_Grimm_Nightmare", "Grimm Control/Grimm Bats/Real Bat"),
                ("GG_Grimm_Nightmare", "Grimm Spike Holder/Nightmare Spike"),
                ("GG_Grimm", "Grimm Spike Holder/Grimm Spike"),
                ("Abyss_02", "Flamebearer Spawn"),
                ("Fungus1_10", "Flamebearer Spawn")
            };
        }
        public static TextureStrings Ts { get; private set; }
        public List<int> CharmIDs { get; private set; }

        public static GameObject? nkg;
        public static GameObject? burst;
        public static GameObject? realBat;
        public static GameObject? grimmkinSpawner;
        public static GameObject? grimmkinSpawnerSmall;
        public static GameObject? nightmareSpike;
        public static GameObject? grimmSpike;
        public static AudioSource? audioSource;
        public static tk2dSpriteAnimation? spikeAnimation;
        
        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            Log("Initializing");
            nkg = preloadedObjects["GG_Grimm_Nightmare"]["Grimm Control/Nightmare Grimm Boss"];
            realBat = preloadedObjects["GG_Grimm_Nightmare"]["Grimm Control/Grimm Bats/Real Bat"];
            nightmareSpike = preloadedObjects["GG_Grimm_Nightmare"]["Grimm Spike Holder/Nightmare Spike"];
            grimmSpike = preloadedObjects["GG_Grimm"]["Grimm Spike Holder/Grimm Spike"];
            grimmkinSpawner = preloadedObjects["Abyss_02"]["Flamebearer Spawn"];
            grimmkinSpawnerSmall = preloadedObjects["Fungus1_10"]["Flamebearer Spawn"];
            GameObject.DontDestroyOnLoad(nkg);
            GameObject.DontDestroyOnLoad(realBat);
            Instance ??= this;
            var go = new GameObject("AudioSource");
            audioSource = go.AddComponent<AudioSource>();
            audioSource.pitch = .75f;
            audioSource.volume = .01f;
            UnityEngine.Object.DontDestroyOnLoad(audioSource);

            CharmIDs = CharmHelper.AddSprites(Ts.Get(TextureStrings.NightmareSparkKey));

            var item = new ItemChanger.Items.CharmItem()
            {
                charmNum = CharmIDs[0],
                name = _charmNames[0],
                UIDef = new MsgUIDef()
                {
                    name = new LanguageString("UI", $"CHARM_NAME_{CharmIDs[0]}"),
                    shopDesc = new LanguageString("UI", $"CHARM_DESC_{CharmIDs[0]}"),
                    sprite = new ICSprite()
                }
            };
            // Tag the item for ConnectionMetadataInjector, so that MapModS and
            // other mods recognize the items we're adding as charms.
            var mapmodTag = item.AddTag<InteropTag>();
            mapmodTag.Message = "RandoSupplementalMetadata";
            mapmodTag.Properties["ModSource"] = GetName();
            mapmodTag.Properties["PoolGroup"] = "Charms";
            Finder.DefineCustomItem(item);

            InitCallbacks();
            On.HeroController.Awake += AddChild;
            On.PlayMakerFSM.Awake += FSMAwake;
           // ModHooks.HeroUpdateHook += test;
            ModHooks.DashPressedHook += Firetrail.StartTrail;
            ModHooks.SetPlayerBoolHook += CheckCharms;
            On.HealthManager.TakeDamage += Firebat.BatDie;
            ModHooks.HeroUpdateHook += ShapeOfGrimm.GrimmSlugMovement;
            ModHooks.FinishedLoadingModsHook += DebugGiveCharm;
            On.DamageEnemies.FixedUpdate += ErrorBegone;
            ModHooks.HeroUpdateHook += GrimmkinWarp.WarpMain;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += GrimmkinWarp.SceneChange;
            On.UIManager.ContinueGame += (On.UIManager.orig_ContinueGame orig, global::UIManager self) =>
            {
                ItemChangerMod.CreateSettingsProfile(overwrite: false, createDefaultModules: false);
                orig(self);
            };
            On.UIManager.StartNewGame += (On.UIManager.orig_StartNewGame orig, UIManager self, bool permaDeath, bool bossRush) =>
            {
                ItemChangerMod.CreateSettingsProfile(overwrite: false, createDefaultModules: false);
                orig(self, permaDeath, bossRush);
            };

            ModHooks.SetPlayerBoolHook += (string target, bool orig) =>
            { 
                var pd = PlayerData.instance;
                if (PlayerDataAccess.bossRushMode)
                {
                    SaveSettings.gotCharms[0] = true;
                }
                if (!SaveSettings.PlacedCharm && !pd.GetBool($"gotCharm_{CharmIDs[0]}") && PlayerDataAccess.destroyedNightmareLantern || !SaveSettings.PlacedCharm && !pd.GetBool($"gotCharm_{CharmIDs[0]}") && PlayerDataAccess.killedNightmareGrimm)
                {
                    float xpos = 47.2f;
                    float ypos = 4.4f;
                    var placements = new List<AbstractPlacement>();
                    var name = _charmNames[0];
                    placements.Add(
                        new CoordinateLocation()
                        {
                            x = xpos,
                            y = ypos,
                            elevation = 0,
                            sceneName = "Cliffs_06",
                            name = name
                        }
                        .Wrap()
                        .Add(Finder.GetItem(name)));
                    ItemChangerMod.AddPlacements(placements, conflictResolution: PlacementConflictResolution.Ignore);
                    SaveSettings.PlacedCharm = true;
                }
                return orig;
            };
            Log("Initialized");
        }
        private void AddChild(On.HeroController.orig_Awake orig, HeroController self)
        {
            var indicatorTorch = GameObject.Instantiate(grimmkinSpawner.gameObject);
            GameObject.Destroy(indicatorTorch.LocateMyFSM("Spawn Control"));
            GameObject.Destroy(indicatorTorch.Find("Hero Detector"));
            indicatorTorch.name = "Torch Indicator";
            indicatorTorch.transform.parent = self.transform;
            indicatorTorch.transform.position = self.transform.position + new Vector3(0.3f, -0.7f, 0);
            indicatorTorch.transform.SetRotationZ(328.8504f);
            indicatorTorch.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            indicatorTorch.Find("Active Effects").Find("Pt Orbs").GetComponent<ParticleSystemRenderer>().maxParticleSize = .01f;
            indicatorTorch.active = false;
            indicatorTorch.Find("Active Effects").Find("Flame_smoke").active = false;
            indicatorTorch.Find("Active Effects").Find("lava_particles_03").active = false;

            var warpTorch = GameObject.Instantiate(grimmkinSpawner.gameObject);
            warpTorch.name = "Warp Torch";
            warpTorch.transform.parent = GameManager.instance.transform.Find("GlobalPool").transform;
            GameObject.Destroy(warpTorch.LocateMyFSM("Spawn Control"));
            GameObject.Destroy(warpTorch.Find("Hero Detector"));
            warpTorch.Find("Active Effects").active = false;
            warpTorch.active = false;

            var grimmkinNovice = Nightmare_Spark.grimmkinSpawnerSmall.LocateMyFSM("Spawn Control").GetState("Level 1").GetAction<CreateObject>(0).gameObject.Value;
            var novice = GameObject.Instantiate(grimmkinNovice);
            GameObject.Destroy(novice.LocateMyFSM("Control"));
            GameObject.Destroy(novice.GetComponent<DamageHero>());
            novice.name = "NoviceObj";
            novice.transform.parent = GameManager.instance.transform.Find("GlobalPool").transform;
            novice.RemoveComponent<HealthManager>();
            novice.RemoveComponent<EnemyDreamnailReaction>();
            novice.AddComponent<NonBouncer>();
            novice.AddComponent<GrimmkinWarp.NoviceBehaviour>();
            novice.layer = (int)PhysLayers.DEFAULT;
            novice.GetComponent<MeshRenderer>().enabled = false;
            novice.Find("Pt Orbs").GetComponent<ParticleSystem>().enableEmission = false;
            novice.Find("Explode Effects").Find("Flame Ring").active = false;
            novice.GetComponent<BoxCollider2D>().enabled = false;
            /*GameObject hazardCollider = new();
            hazardCollider.name = "hazardCollider";
            hazardCollider.transform.parent = novice.transform;
            hazardCollider.layer = (int)PhysLayers.HERO_BOX;
            hazardCollider.AddComponent<BoxCollider2D>().isTrigger = true;
            hazardCollider.GetComponent<BoxCollider2D>().size = new Vector2(1.5f, 1);*/
            GrimmkinWarp.noviceObj = novice;

            orig(self);
        }

        private void ErrorBegone(On.DamageEnemies.orig_FixedUpdate orig, DamageEnemies self)
        {
            try
            {
                orig(self);
            }
            catch { }        
        }

        #region Charm Setup
        private void InitCallbacks()
        {
            ModHooks.GetPlayerBoolHook += OnGetPlayerBoolHook;
            ModHooks.SetPlayerBoolHook += OnSetPlayerBoolHook;
            ModHooks.GetPlayerIntHook += OnGetPlayerIntHook;
            ModHooks.AfterSavegameLoadHook += InitSaveSettings;
            ModHooks.LanguageGetHook += OnLanguageGetHook;

        }
        private void InitSaveSettings(SaveGameData data)
        {
            // Found in a project, might help saving, don't know, but who cares
            // Charms
            SaveSettings.gotCharms = SaveSettings.gotCharms;
            SaveSettings.newCharms = SaveSettings.newCharms;
            SaveSettings.equippedCharms = SaveSettings.equippedCharms;
            SaveSettings.charmCosts = SaveSettings.charmCosts;
            SaveSettings.dP = SaveSettings.dP; //Dwarf Pogo :dwarfwoot:
        }

        private readonly string[] _charmNames =
        {
            "Nightmare Spark",

        };
        private readonly string[] _charmDescriptions =
        {
            "A remnant of the Nightmare King's power,<br>still resonating with the everburning fire of the Troupe.<br><br>Dashing leaves a fire trail behind which<br>can damage enemies",

        };
        private string OnLanguageGetHook(string key, string sheet, string orig)
        {
            if (key.StartsWith("CHARM_NAME_"))
            {
                int charmNum = int.Parse(key.Split('_')[2]);
                if (CharmIDs.Contains(charmNum))
                {
                    return _charmNames[CharmIDs.IndexOf(charmNum)];
                }
            }
            else if (key.StartsWith("CHARM_DESC_"))
            {
                int charmNum = int.Parse(key.Split('_')[2]);
                if (CharmIDs.Contains(charmNum))
                {
                    return _charmDescriptions[CharmIDs.IndexOf(charmNum)];
                }
            }
            return orig;
        }
        private bool OnGetPlayerBoolHook(string target, bool orig)
        {
            if (target.StartsWith("gotCharm_"))
            {
                int charmNum = int.Parse(target.Split('_')[1]);
                if (CharmIDs.Contains(charmNum))
                {
                    return SaveSettings.gotCharms[CharmIDs.IndexOf(charmNum)];
                }
            }
            if (target.StartsWith("newCharm_"))
            {
                int charmNum = int.Parse(target.Split('_')[1]);
                if (CharmIDs.Contains(charmNum))
                {
                    return SaveSettings.newCharms[CharmIDs.IndexOf(charmNum)];
                }
            }
            if (target.StartsWith("equippedCharm_"))
            {
                int charmNum = int.Parse(target.Split('_')[1]);
                if (CharmIDs.Contains(charmNum))
                {
                    return SaveSettings.equippedCharms[CharmIDs.IndexOf(charmNum)];
                }
            }
            return orig;
        }
        private bool OnSetPlayerBoolHook(string target, bool orig)
        {
            if (target.StartsWith("gotCharm_"))
            {
                int charmNum = int.Parse(target.Split('_')[1]);
                if (CharmIDs.Contains(charmNum))
                {
                    SaveSettings.gotCharms[CharmIDs.IndexOf(charmNum)] = orig;
                    return orig;
                }
            }
            if (target.StartsWith("newCharm_"))
            {
                int charmNum = int.Parse(target.Split('_')[1]);
                if (CharmIDs.Contains(charmNum))
                {
                    SaveSettings.newCharms[CharmIDs.IndexOf(charmNum)] = orig;
                    return orig;
                }
            }
            if (target.StartsWith("equippedCharm_"))
            {
                int charmNum = int.Parse(target.Split('_')[1]);
                if (CharmIDs.Contains(charmNum))
                {
                    SaveSettings.equippedCharms[CharmIDs.IndexOf(charmNum)] = orig;
                    return orig;
                }
            }
            return orig;
        }
        private int OnGetPlayerIntHook(string target, int orig)
        {
            if (target.StartsWith("charmCost_"))
            {
                int charmNum = int.Parse(target.Split('_')[1]);
                if (CharmIDs.Contains(charmNum))
                {
                    return SaveSettings.charmCosts[CharmIDs.IndexOf(charmNum)];
                }
            }
            return orig;
        }

        #endregion



        private void FSMAwake(On.PlayMakerFSM.orig_Awake orig, PlayMakerFSM self)
        {
            orig(self);
            if (self.FsmName == "Control")
            {
                if (self.gameObject.tag == "Grimmchild")
                {
                    FsmState grimmchild = self.GetState("Antic");
                    grimmchild.InsertCustomAction("Antic", () => Grimmchild.GrimmchildMain(self.gameObject), 7);
                }

            }
            if (self.FsmName == "Spell Control")
            {
                FsmState castShadeSoul = self.GetState("Fireball 2");
                castShadeSoul.InsertCustomAction("Fireball 2", () => Firebat.SpawnBat(20), 4);
                FsmState castVengefulSpirit = self.GetState("Fireball 1");
                castVengefulSpirit.InsertCustomAction("Fireball 1", () => Firebat.SpawnBat(15), 4);
                FsmState castQuakeDive = self.GetState("Q1 Effect");
                castQuakeDive.InsertCustomAction("Q1 Effect", () => DiveFireball.DiveFireballs(13, 24), 4);
                FsmState castQuakeDark = self.GetState("Q2 Effect");
                castQuakeDark.InsertCustomAction("Q2 Effect", () => DiveFireball.DiveFireballs(18, 36), 4);
                FsmState castSlug = self.GetState("Focus S");
                castSlug.InsertCustomAction("Focus S", () => ShapeOfGrimm.GrimmSlug(), 15);
            }
            if (self.gameObject.name == "Knight" && self.FsmName == "Roar Lock")
            {
                self.GetState("Lock Start").InsertCustomAction(() => ShapeOfGrimm.cancelGs = true, 10);
                self.GetState("Regain Control").InsertCustomAction(() => ShapeOfGrimm.cancelGs = false, 3);
            }
            if (self.gameObject.name == "Final Boss Door" && self.FsmName == "Control")
            {
                self.GetState("Take Control").InsertCustomAction(() => ShapeOfGrimm.cancelGs = true, 10);
                self.GetState("End").InsertCustomAction(() => ShapeOfGrimm.cancelGs = false, 4);
            }
            if (self.gameObject.name == "Hornet Fountain Encounter" && self.FsmName == "Control")
            {
                self.GetState("Take Control").InsertCustomAction(() => ShapeOfGrimm.cancelGs = true, 11);
                self.GetState("End").InsertCustomAction(() => ShapeOfGrimm.cancelGs = false, 5);
            }
            if (self.gameObject.name == "Grimm Scene" && self.FsmName == "Initial Scene")
            {
                self.GetState("Take Control").InsertCustomAction(() => ShapeOfGrimm.cancelGs = true, 11);
                self.GetState("End").InsertCustomAction(() => ShapeOfGrimm.cancelGs = false, 6);
            }
        }
        public bool sdashtransition = false;
        private bool CheckCharms(string target, bool orig)
        {
            if (HeroController.instance == null || HeroController.instance.spellControl == null) { return orig; }
            //--------Grimmkin Warp--------//

            var sdash = HeroController.instance.superDash;
            
            if (PlayerData.instance.GetBool($"equippedCharm_{CharmIDs[0]}") && PlayerDataAccess.equippedCharm_37)
            {
                if (!sdashtransition)
                {
                    sdashtransition = true;
                    sdash.RemoveTransition("Inactive", "BUTTON DOWN");
                }
                
            }
            else
            {
                if (sdashtransition)
                {
                    sdashtransition = false;
                    sdash.AddTransition("Inactive", "BUTTON DOWN", "Can Superdash?");
                }
                
            }
            
            

            //--------Fireball Dive--------//

           
            FsmState castQuakeDive = HeroController.instance.spellControl.GetState("Q1 Effect");
            FsmState castQuakeDark = HeroController.instance.spellControl.GetState("Q2 Effect");
            if (PlayerData.instance.GetBool($"equippedCharm_{CharmIDs[0]}") && PlayerDataAccess.equippedCharm_33)
            {
                castQuakeDive.GetAction<CustomFsmAction>(4).Enabled = true;
                castQuakeDark.GetAction<CustomFsmAction>(4).Enabled = true;
            }
            else
            {
                castQuakeDive.GetAction<CustomFsmAction>(4).Enabled = false;
                castQuakeDark.GetAction<CustomFsmAction>(4).Enabled = false;
            }

            //--------Firebat Spell--------//

            FsmState castShadeSoul = HeroController.instance.spellControl.GetState("Fireball 2");
            FsmState castVengefulSpirit = HeroController.instance.spellControl.GetState("Fireball 1");
            if (PlayerDataAccess.equippedCharm_11 && PlayerData.instance.GetBool($"equippedCharm_{CharmIDs[0]}"))
            {
                castShadeSoul.GetAction<SpawnObjectFromGlobalPool>(3).Enabled = false;
                castShadeSoul.GetAction<CustomFsmAction>(4).Enabled = true;
                castVengefulSpirit.GetAction<SpawnObjectFromGlobalPool>(3).Enabled = false;
                castVengefulSpirit.GetAction<CustomFsmAction>(4).Enabled = true;


            }
            else
            {
                castShadeSoul.GetAction<SpawnObjectFromGlobalPool>(3).Enabled = true;
                castShadeSoul.GetAction<CustomFsmAction>(4).Enabled = false;
                castVengefulSpirit.GetAction<SpawnObjectFromGlobalPool>(3).Enabled = true;
                castVengefulSpirit.GetAction<CustomFsmAction>(4).Enabled = false;
            }

            //--------Grimmchild--------//

            int gcLevel = PlayerDataAccess.grimmChildLevel;
            if (PlayerData.instance.GetBool($"equippedCharm_{CharmIDs[0]}") && PlayerDataAccess.equippedCharm_40 && gcLevel <= 4 && gcLevel > 1)
            {
               
                PlayMakerFSM grimmchild = GameObject.FindWithTag("Grimmchild").LocateMyFSM("Control");
                if (grimmchild != null)
                {
                    
                    grimmchild.GetState("Antic").GetAction<CustomFsmAction>(7).Enabled = true;
                }

            }
            else
            {
                var gc = HeroController.instance.transform.Find("Charm Effects").gameObject.LocateMyFSM("Spawn Grimmchild");
                PlayMakerFSM grimmchild = GameObject.FindWithTag("Grimmchild").LocateMyFSM("Control");
                if (grimmchild != null)
                {
                    gc.FsmVariables.FindFsmGameObject("Child").Value.Find("Enemy Range").transform.localScale = new Vector3(1f, 1f, 1f);
                    grimmchild.GetState("Antic").GetAction<CustomFsmAction>(7).Enabled = false;
                }
            }


            //--------Carefree/Thorns--------//
            if (PlayerData.instance.GetBool($"equippedCharm_{CharmIDs[0]}") && gcLevel == 5 && PlayerDataAccess.equippedCharm_40)
            {
                if (PlayerDataAccess.equippedCharm_12)
                {
                    CarefreeSpikes.NightmareSpikeActivate();
                }
                else
                {
                    CarefreeSpikes.GrimmSpikeActivate();
                }

            }

            //--------Grimm Slug--------//
 
            var sc = HeroController.instance.spellControl;
            FsmState castSlug = HeroController.instance.spellControl.GetState("Focus S");
            if (PlayerDataAccess.equippedCharm_28 && PlayerData.instance.GetBool($"equippedCharm_{CharmIDs[0]}"))
            {
                castSlug.GetAction<CustomFsmAction>(15).Enabled = true;
            }
            else
            {
                castSlug.GetAction<CustomFsmAction>(15).Enabled = false;
            }
            return orig;

        }


        public class ICSprite : ISprite
        {
            public Sprite Value { get; } = Ts.Get(TextureStrings.NightmareSparkKey);
            public ISprite Clone() => (ISprite)MemberwiseClone();
        }
        private void DebugGiveCharm()
        {
            if (ModHooks.GetMod("DebugMod") is Mod)

            {
                var commands = Type.GetType("DebugMod.BindableFunctions, DebugMod");
                if (commands == null)
                {
                    return;
                }
                var method = commands.GetMethod("GiveAllCharms", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    return;
                }
                new Hook(
                    method,
                    (Action orig) =>
                    {
                        SaveSettings.gotCharms[0] = true;
                        SaveSettings.equippedCharms[0] = false;
                        orig();

                    }
                );
                var methodRemove = commands.GetMethod("RemoveAllCharms", BindingFlags.Public | BindingFlags.Static);
                if (methodRemove == null)
                {
                    return;
                }
                new Hook(
                    methodRemove,
                    (Action orig) =>
                    {
                        SaveSettings.gotCharms[0] = false;
                        orig();

                    }
                );
            }
        }

        public static DamageEnemies AddDamageEnemy(GameObject go)
        {
            var dmg = go.GetAddComponent<DamageEnemies>();
            dmg.attackType = AttackTypes.Spell;
            dmg.circleDirection = false;
            dmg.damageDealt = 15;
            dmg.direction = 90 * 3;
            dmg.ignoreInvuln = false;
            dmg.magnitudeMult = 1f;
            dmg.moveDirection = false;
            dmg.specialType = 0;
            return dmg;
        }
    }
}