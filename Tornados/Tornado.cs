using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using System.Reflection;

namespace Disasters
{
    [StaticConstructorOnStartup]
    public static class TornadoHarmonyLoader {
        static TornadoHarmonyLoader()
        {
            var harmony = new Harmony("net.mseal.rimworld.mod.tornado");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(Tornado), "Tick")]
    internal static class Map_FinalizeInit_Patch {
        private static void Postfix(Tornado __instance, ref int ___leftFadeOutTicks, Vector2 ___realPosition)
        {
            if (__instance.Spawned && ___leftFadeOutTicks <= 0)
            {
                IntVec3 intVec = new Vector3(___realPosition.x, 0f, ___realPosition.y).ToIntVec3();
                if (intVec.InBounds(__instance.Map))
                {
                    RoofDef roofDef = __instance.Map.roofGrid.RoofAt(intVec);
                    if (roofDef != null && roofDef.isThickRoof)
                    {
                        ___leftFadeOutTicks = 120;
                        Messages.Message("MessageTornadoDissipated".Translate(), new TargetInfo(__instance.Position, __instance.Map, false), MessageTypeDefOf.PositiveEvent, true);
                    }
                }
            }
        }
    }

    public class TornadoLoader : ModBase
    {
        const float DEFAULT_BASE_CHANCE = 0.25F;
        const int DEFAULT_MIN_REFIRE = 15;

        public static SettingHandle<float> baseChanceHandler;
        public static SettingHandle<int> minRefireDaysHandler;

        public static SettingHandle.ValueIsValid FloatNonNegativeValidator()
        {
            return str => {
                float parsed;
                if (!float.TryParse(str, out parsed)) return false;
                return parsed >= 0.0;
            };
        }

        public static SettingHandle.ValueIsValid IntNonNegativeValidator()
        {
            return str => {
                int parsed;
                if (!int.TryParse(str, out parsed)) return false;
                return parsed >= 0;
            };
        }

        public override string ModIdentifier
        {
            get { return "DisastersTornado"; }
        }

        public override void Initialize()
        {
            // add a mod name to display in the Mods Settings menu
            Settings.EntryName = "Tornado.SettingName".Translate();
        }

        protected void LoadHandles()
        {
            baseChanceHandler = Settings.GetHandle<float>(
                "baseChance",
                "Tornado.BaseChance".Translate(),
                "Tornado.BaseChanceDescription".Translate(),
                DEFAULT_BASE_CHANCE,
                FloatNonNegativeValidator());
            baseChanceHandler.ValueChanged += newValue => { ApplySettings(); };

            minRefireDaysHandler = Settings.GetHandle<int>(
                "minRefireDays",
                "Tornado.MinRefire".Translate(),
                "Tornado.MinRefireDescription".Translate(),
                DEFAULT_MIN_REFIRE,
                IntNonNegativeValidator());
            minRefireDaysHandler.ValueChanged += newValue => { ApplySettings(); };
        }

        public void ApplySettings()
        {
            IncidentDef tornado = IncidentDef.Named("Tornado");
            tornado.baseChance = baseChanceHandler;
            tornado.minRefireDays = minRefireDaysHandler;

            Logger.Message(String.Format(
                "Settings loaded:\n\tbaseChange: {0}\n\tminRefireDays: {1}",
                tornado.baseChance, tornado.minRefireDays));
        }

        public override void DefsLoaded()
        {
            LoadHandles();
            ApplySettings();
        }
    }

    /*
    * Copied from decompilation from B18
    */
    public class IncidentWorker_Tornado : IncidentWorker {
        protected const int MinDistanceFromMapEdge = 30;

        protected const float MinWind = 1f;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return true;
        }

        protected virtual Tornado Spawn(IntVec3 loc, Map map)
        {
            return (Tornado)GenSpawn.Spawn(ThingDef.Named("Tornado"), loc, map);
        }

        // Extracted from TryExecuteWorker
        protected virtual Tornado TrySpawnOnMap(Map map)
        {
            CellRect cellRect = CellRect.WholeMap(map).ContractedBy(30);
            if (cellRect.IsEmpty)
            {
                cellRect = CellRect.WholeMap(map);
            }
            IntVec3 loc;
            if (!CellFinder.TryFindRandomCellInsideWith(cellRect, (IntVec3 x) => this.CanSpawnTornadoAt(x, map), out loc))
            {
                return null;
            }
            return Spawn(loc, map);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Tornado tornado = TrySpawnOnMap(map);
            if (tornado == null) { return false; }
            base.SendStandardLetter(parms, tornado);
            return true;
        }

        protected bool CanSpawnTornadoAt(IntVec3 c, Map map)
        {
            if (c.Fogged(map))
            {
                return false;
            }
            int num = GenRadial.NumCellsInRadius(7f);
            for (int i = 0; i < num; i++)
            {
                IntVec3 c2 = c + GenRadial.RadialPattern[i];
                if (c2.InBounds(map))
                {
                    if (this.AnyPawnOfPlayerFactionAt(c2, map))
                    {
                        return false;
                    }
                    // Added to eliminate spawning inside areas that will immediate despawn
                    RoofDef roofDef = map.roofGrid.RoofAt(c2);
                    if (roofDef != null && roofDef.isThickRoof)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        protected bool AnyPawnOfPlayerFactionAt(IntVec3 c, Map map)
        {
            List<Thing> thingList = c.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Pawn pawn = thingList[i] as Pawn;
                if (pawn != null && pawn.Faction == Faction.OfPlayer)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
