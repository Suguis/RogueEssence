﻿using System;
using System.Collections.Generic;
using RogueEssence.Content;
using RogueElements;
using RogueEssence.Data;

namespace RogueEssence.Dungeon
{
    public partial class DungeonScene
    {
        public IEnumerator<YieldInstruction> CancelWait(Loc loc)
        {
            if (AnimationsOver())
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20, loc));
        }

        public IEnumerator<YieldInstruction> ProcessUseSkill(Character character, int skillSlot, ActionResult result)
        {
            BattleContext context = new BattleContext(BattleActionType.Skill);
            context.User = character;
            context.UsageSlot = skillSlot;

            yield return CoroutineManager.Instance.StartCoroutine(InitActionData(context));
            yield return CoroutineManager.Instance.StartCoroutine(context.User.BeforeTryAction(context));
            if (context.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(CancelWait(context.User.CharLoc)); yield break; }

            result.Success = ActionResult.ResultType.TurnTaken;

            //move has been made; end-turn must be done from this point onwards
            yield return CoroutineManager.Instance.StartCoroutine(CheckExecuteAction(context, PreExecuteSkill));

            if (context.SkillUsedUp > -1 && !context.User.Dead)
            {
                SkillData entry = DataManager.Instance.GetSkill(context.SkillUsedUp);
                LogMsg(Text.FormatKey("MSG_OUT_OF_CHARGES", context.User.Name, entry.Name.ToLocal()));

                yield return CoroutineManager.Instance.StartCoroutine(DungeonScene.Instance.ProcessBattleFX(context.User, context.User, DataManager.Instance.NoChargeFX));
            }

            if (!context.CancelState.Cancel)
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20, context.User.CharLoc));
            yield return CoroutineManager.Instance.StartCoroutine(FinishTurn(context.User));
        }


        public IEnumerator<YieldInstruction> ProcessUseItem(Character character, int invSlot, int teamSlot, ActionResult result)
        {
            if (character.AttackOnly)
            {
                LogMsg(Text.FormatKey("MSG_CANT_USE_ITEM", character.Name), false, true);
                yield break;
            }
            Character target = teamSlot == -1 ? character : character.MemberTeam.Players[teamSlot];
            if (target.AttackOnly)
            {
                LogMsg(Text.FormatKey("MSG_CANT_USE_ITEM", target.Name), false, true);
                yield break;
            }

            BattleContext context = new BattleContext(BattleActionType.Item);
            context.User = target;
            context.UsageSlot = invSlot;

            yield return CoroutineManager.Instance.StartCoroutine(InitActionData(context));
            yield return CoroutineManager.Instance.StartCoroutine(context.User.BeforeTryAction(context));
            if (context.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(CancelWait(context.User.CharLoc)); yield break; }

            result.Success = ActionResult.ResultType.TurnTaken;

            //move has been made; end-turn must be done from this point onwards
            yield return CoroutineManager.Instance.StartCoroutine(CheckExecuteAction(context, PreExecuteItem));
            if (!context.CancelState.Cancel)
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20, context.User.CharLoc));
            yield return CoroutineManager.Instance.StartCoroutine(FinishTurn(context.User));
        }

        public IEnumerator<YieldInstruction> ProcessThrowItem(Character character, int invSlot, ActionResult result)
        {
            if (character.AttackOnly)
            {
                LogMsg(Text.FormatKey("MSG_CANT_USE_ITEM", character.Name), false, true);
                yield break;
            }

            BattleContext context = new BattleContext(BattleActionType.Throw);
            context.User = character;
            context.UsageSlot = invSlot;

            yield return CoroutineManager.Instance.StartCoroutine(InitActionData(context));
            yield return CoroutineManager.Instance.StartCoroutine(context.User.BeforeTryAction(context));
            if (context.CancelState.Cancel) { yield return CoroutineManager.Instance.StartCoroutine(CancelWait(context.User.CharLoc)); yield break; }

            result.Success = ActionResult.ResultType.TurnTaken;

            //move has been made; end-turn must be done from this point onwards
            yield return CoroutineManager.Instance.StartCoroutine(CheckExecuteAction(context, PreExecuteItem));
            if (!context.CancelState.Cancel)
                yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20, context.User.CharLoc));
            yield return CoroutineManager.Instance.StartCoroutine(FinishTurn(context.User));
        }


        public IEnumerator<YieldInstruction> InitActionData(BattleContext context)
        {
            EventEnqueueFunction<BattleEvent> function = (StablePriorityQueue<GameEventPriority, Tuple<GameEventOwner, Character, BattleEvent>> queue, int maxPriority, ref int nextPriority) =>
            {
                DataManager.Instance.UniversalEvent.AddEventsToQueue(queue, maxPriority, ref nextPriority, DataManager.Instance.UniversalEvent.InitActionData);
            };
            foreach (Tuple<GameEventOwner, Character, BattleEvent> effect in IterateEvents(function))
            {
                yield return CoroutineManager.Instance.StartCoroutine(effect.Item3.Apply(effect.Item1, effect.Item2, context));
                if (context.CancelState.Cancel) yield break;
            }
        }

        public IEnumerator<YieldInstruction> PreExecuteSkill(BattleContext context)
        {
            if (context.UsageSlot > BattleContext.DEFAULT_ATTACK_SLOT && context.UsageSlot < CharData.MAX_SKILL_SLOTS)
            {
                yield return CoroutineManager.Instance.StartCoroutine(context.User.DeductCharges(context.UsageSlot, 1, false, false));
                if (context.User.Skills[context.UsageSlot].Element.Charges == 0)
                    context.SkillUsedUp = context.User.Skills[context.UsageSlot].Element.SkillNum;
            }
            yield return new WaitUntil(AnimationsOver);

            if (!String.IsNullOrEmpty(context.actionMsg))
                LogMsg(context.actionMsg);

            yield break;
        }

        public IEnumerator<YieldInstruction> PreExecuteItem(BattleContext context)
        {
            //remove the item from the inventory/ground/hold
            if (context.UsageSlot > BattleContext.EQUIP_ITEM_SLOT)
            {
                InvItem item = ((ExplorerTeam)context.User.MemberTeam).Inventory[context.UsageSlot];
                ItemData entry = (ItemData)item.GetData();
                if (entry.MaxStack > 1 && item.HiddenValue > 1)
                    item.HiddenValue--;
                else
                    ((ExplorerTeam)context.User.MemberTeam).Inventory.RemoveAt(context.UsageSlot);
            }
            else if (context.UsageSlot == BattleContext.EQUIP_ITEM_SLOT)
            {
                InvItem item = context.User.EquippedItem;
                ItemData entry = (ItemData)item.GetData();
                if (entry.MaxStack > 1 && item.HiddenValue > 1)
                    item.HiddenValue--;
                else
                    context.User.DequipItem();
            }
            else if (context.UsageSlot == BattleContext.FLOOR_ITEM_SLOT)
            {
                int mapSlot = ZoneManager.Instance.CurrentMap.GetItem(context.User.CharLoc);
                MapItem item = ZoneManager.Instance.CurrentMap.Items[mapSlot];
                ItemData entry = DataManager.Instance.GetItem(item.Value);
                if (entry.MaxStack > 1 && item.HiddenValue > 1)
                    item.HiddenValue--;
                else
                    ZoneManager.Instance.CurrentMap.Items.RemoveAt(mapSlot);
            }

            yield return new WaitUntil(AnimationsOver);

            if (!String.IsNullOrEmpty(context.actionMsg))
                LogMsg(context.actionMsg);

            yield break;
        }



        public IEnumerator<YieldInstruction> PerformAction(BattleContext context)
        {
            //this is where the delays between target hits are managed
            context.HitboxAction.Distance += Math.Min(Math.Max(-3, context.RangeMod), 3);
            yield return CoroutineManager.Instance.StartCoroutine(context.User.PerformCharAction(context.HitboxAction.Clone(), context));
            //if (context.User.CharLoc == context.StrikeEndTile && context.StrikeEndTile != context.StrikeStartTile)
            //    yield return CoroutinesManager.Instance.StartCoroutine(ArriveOnTile(context.User, false, false, false));

            //TODO: test to make sure everything is consistent with the erasure of handling movement in here
            //first, splash needs to work properly, because the hopping will call its own tile landing
            //dash attacks will also handle their own tile landing.
            //for now, since all dash moves hit tiles, that means they will all activate the tile

            //TODO: make NOTRAP true, and then make the attacks themselves activate traps
            //TODO: need to make sure that the user lands on their given tile EXACTLY ONCE
            //ex:
            //action does not move the user; move effect does not move the user //most moves
            //the action effect nor the main flow should not handle the update
            //action does not move the user; move effect DOES move the user //hopping moves
            //the action effect must handle the update; the main flow should not
            //action DOES move the user; move effect does not move the user //a dash attack
            //the action effect must not handle any update; the main flow should.
            //action DOES move the user; move effect DOES move the user //???
            //the action effect handles the update; the main flow doesn't need to
        }

        public delegate IEnumerator<YieldInstruction> ContextMethod(BattleContext context);

        public IEnumerator<YieldInstruction> CheckExecuteAction(BattleContext context, ContextMethod preExecute)
        {
            yield return CoroutineManager.Instance.StartCoroutine(PreProcessAction(context));
            yield return CoroutineManager.Instance.StartCoroutine(context.User.BeforeAction(context));
            if (context.CancelState.Cancel) yield break;
            yield return CoroutineManager.Instance.StartCoroutine(preExecute(context));
            yield return CoroutineManager.Instance.StartCoroutine(ExecuteAction(context));
            if (context.CancelState.Cancel) yield break;
            yield return CoroutineManager.Instance.StartCoroutine(RepeatActions(context));
        }



        public IEnumerator<YieldInstruction> PreProcessAction(BattleContext context)
        {
            context.StrikeStartTile = context.User.CharLoc;
            context.StrikeEndTile = context.User.CharLoc;

            List<Dir8> trialDirs = new List<Dir8>();
            trialDirs.Add(context.User.CharDir);

            if (context.UsageSlot != BattleContext.FORCED_SLOT && context.User.MovesScrambled)
            {
                trialDirs.Add(DirExt.AddAngles(context.User.CharDir, Dir8.DownRight));
                trialDirs.Add(DirExt.AddAngles(context.User.CharDir, Dir8.DownLeft));
            }
            ProcessDir(trialDirs[DataManager.Instance.Save.Rand.Next(trialDirs.Count)], context.User);

            yield break;
        }


        public IEnumerator<YieldInstruction> ExecuteAction(BattleContext baseContext)
        {
            BattleContext context = new BattleContext(baseContext, true);

            yield return CoroutineManager.Instance.StartCoroutine(context.User.OnAction(context));
            if (context.CancelState.Cancel) yield break;
            yield return CoroutineManager.Instance.StartCoroutine(PerformAction(context));
            if (context.CancelState.Cancel) yield break;
            yield return CoroutineManager.Instance.StartCoroutine(context.User.AfterActionTaken(context));
        }

        public IEnumerator<YieldInstruction> RepeatActions(BattleContext context)
        {
            //increment for multistrike
            context.StrikesMade++;
            while (context.StrikesMade < context.Strikes && !context.User.Dead)
            {
                yield return CoroutineManager.Instance.StartCoroutine(PreProcessAction(context));
                yield return CoroutineManager.Instance.StartCoroutine(context.User.BeforeAction(context));
                if (context.CancelState.Cancel) yield break;
                yield return CoroutineManager.Instance.StartCoroutine(ExecuteAction(context));

                context.StrikesMade++;
            }
        }



        public delegate IEnumerator<YieldInstruction> HitboxEffect(Loc target);


        public IEnumerator<YieldInstruction> MockHitLoc(Loc loc)
        {
            yield break;
            //TODO: draw error VFX on the location?
        }

        public IEnumerator<YieldInstruction> BeforeExplosion(BattleContext context)
        {
            int maxPriority = Int32.MinValue;
            while (true)
            {
                int nextPriority = Int32.MaxValue;

                StablePriorityQueue<GameEventPriority, IEnumerator<YieldInstruction>> instructionQueue = new StablePriorityQueue<GameEventPriority, IEnumerator<YieldInstruction>>();

                context.Data.EnqueueBeforeExplosion(instructionQueue, maxPriority, ref nextPriority, context);

                StablePriorityQueue<int, Character> charQueue = new StablePriorityQueue<int, Character>();
                foreach (Character character in ZoneManager.Instance.CurrentMap.IterateCharacters())
                {
                    if (!character.Dead)
                        charQueue.Enqueue(-character.Speed, character);
                }
                int totalPriority = 0;

                while (charQueue.Count > 0)
                {
                    Character character = charQueue.Dequeue();
                    character.EnqueueBeforeExplosion(instructionQueue, maxPriority, ref nextPriority, totalPriority, context);
                    totalPriority++;
                }

                if (instructionQueue.Count == 0)
                    break;
                else
                {
                    while (instructionQueue.Count > 0)
                    {
                        IEnumerator<YieldInstruction> effect = instructionQueue.Dequeue();
                        yield return CoroutineManager.Instance.StartCoroutine(effect);
                        if (context.CancelState.Cancel)
                            yield break;
                    }
                    if (nextPriority == Int32.MaxValue)
                        break;
                    else
                        maxPriority = nextPriority + 1;
                }
            }
        }

        public IEnumerator<YieldInstruction> BeforeHit(BattleContext context)
        {
            int maxPriority = Int32.MinValue;
            while (true)
            {
                int nextPriority = Int32.MaxValue;

                StablePriorityQueue<GameEventPriority, IEnumerator<YieldInstruction>> instructionQueue = new StablePriorityQueue<GameEventPriority, IEnumerator<YieldInstruction>>();

                if (context.ActionType != BattleActionType.Trap)
                    context.User.EnqueueBeforeHitting(instructionQueue, maxPriority, ref nextPriority, context);
                context.Target.EnqueueBeforeBeingHit(instructionQueue, maxPriority, ref nextPriority, context);
                
                if (instructionQueue.Count == 0)
                    break;
                else
                {
                    while (instructionQueue.Count > 0)
                    {
                        IEnumerator<YieldInstruction> effect = instructionQueue.Dequeue();
                        yield return CoroutineManager.Instance.StartCoroutine(effect);
                        if (context.CancelState.Cancel)
                            yield break;
                    }
                    if (nextPriority == Int32.MaxValue)
                        break;
                    else
                        maxPriority = nextPriority + 1;
                }
            }
        }

        public IEnumerator<YieldInstruction> HitTarget(BattleContext context, Character target)
        {
            yield return CoroutineManager.Instance.StartCoroutine(BeforeHit(context));

            if (context.Hit)
                yield return CoroutineManager.Instance.StartCoroutine(ProcessHit(context));
        }


        public static int GetEffectiveness(Character attacker, Character target, BattleData action, int type)
        {
            int effectiveness = GetEffectiveness(attacker, target, action.Element, type);

            action.ModifyElementEffect(type, ref effectiveness);

            return effectiveness;
        }

        public static int GetEffectiveness(Character attacker, Character target, int attacking, int defending)
        {
            int effectiveness = 0;

            //start with universal
            EventEnqueueFunction<ElementEffectEvent> function = (StablePriorityQueue<GameEventPriority, Tuple<GameEventOwner, Character, ElementEffectEvent>> queue, int maxPriority, ref int nextPriority) =>
            {
                DataManager.Instance.UniversalEvent.AddEventsToQueue(queue, maxPriority, ref nextPriority, DataManager.Instance.UniversalEvent.ElementEffects);
            };
            foreach (Tuple<GameEventOwner, Character, ElementEffectEvent> effect in IterateEvents<ElementEffectEvent>(function))
                effect.Item3.Apply(effect.Item1, effect.Item2, attacking, attacking, ref effectiveness);


            //go through all statuses' element matchup methods
            if (attacker != null)
                attacker.ModifyUserElementEffect(attacking, defending, ref effectiveness);
            if (target != null)
                target.ModifyTargetElementEffect(attacking, defending, ref effectiveness);

            return effectiveness;
        }

        public IEnumerator<YieldInstruction> ProcessEndAnim(Character user, Character target, BattleData data)
        {

            //trigger animations of target
            if (data.HitCharAnim != 0)
            {
                StaticCharAnimation charAnim = CharAnimation.GetCharAnim(data.HitCharAnim);
                charAnim.AnimLoc = target.CharLoc;
                charAnim.CharDir = target.CharDir;
                yield return CoroutineManager.Instance.StartCoroutine(target.StartAnim(charAnim));
            }


            //play battle FX
            foreach (BattleFX fx in data.IntroFX)
                yield return CoroutineManager.Instance.StartCoroutine(ProcessBattleFX(user, target, fx));

            //play sound
            GameManager.Instance.BattleSE(data.HitFX.Sound);
            //the animation
            FiniteEmitter endEmitter = (FiniteEmitter)data.HitFX.Emitter.Clone();
            endEmitter.SetupEmit(target.MapLoc, user.MapLoc, target.CharDir);
            CreateAnim(endEmitter, DrawLayer.NoDraw);
            SetScreenShake(new ScreenMover(data.HitFX.ScreenMovement));
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(data.HitFX.Delay, target.CharLoc));
        }



        public IEnumerator<YieldInstruction> ProcessBattleFX(Character user, Character target, BattleFX fx)
        {
            yield return CoroutineManager.Instance.StartCoroutine(ProcessBattleFX(user.MapLoc, target.MapLoc, target.CharDir, fx));
        }
        public IEnumerator<YieldInstruction> ProcessBattleFX(Loc userLoc, Loc targetLoc, Dir8 userDir, BattleFX fx)
        {
            //play sound
            GameManager.Instance.BattleSE(fx.Sound);
            //the animation
            FiniteEmitter fxEmitter = (FiniteEmitter)fx.Emitter.Clone();
            fxEmitter.SetupEmit(targetLoc, userLoc, userDir);
            CreateAnim(fxEmitter, DrawLayer.NoDraw);
            SetScreenShake(new ScreenMover(fx.ScreenMovement));
            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(fx.Delay, targetLoc));
        }


        public IEnumerator<YieldInstruction> ProcessHit(BattleContext context)
        {
            yield return CoroutineManager.Instance.StartCoroutine(context.Data.Hit(context));


            int maxPriority = Int32.MinValue;
            while (true)
            {
                int nextPriority = Int32.MaxValue;

                StablePriorityQueue<GameEventPriority, IEnumerator<YieldInstruction>> instructionQueue = new StablePriorityQueue<GameEventPriority, IEnumerator<YieldInstruction>>();

                if (context.ActionType != BattleActionType.Trap)
                    context.User.EnqueueAfterHitting(instructionQueue, maxPriority, ref nextPriority, context);
                context.Target.EnqueueAfterBeingHit(instructionQueue, maxPriority, ref nextPriority, context);

                if (instructionQueue.Count == 0)
                    break;
                else
                {
                    while (instructionQueue.Count > 0)
                    {
                        IEnumerator<YieldInstruction> effect = instructionQueue.Dequeue();
                        yield return CoroutineManager.Instance.StartCoroutine(effect);
                        if (context.CancelState.Cancel)
                            yield break;
                    }
                    if (nextPriority == Int32.MaxValue)
                        break;
                    else
                        maxPriority = nextPriority + 1;
                }
            }
        }

        public IEnumerator<YieldInstruction> ReleaseHitboxes(Character user, Hitbox hitbox, HitboxEffect hitboxEffect, HitboxEffect tileEffect)
        {
            List<Hitbox> hitboxes = new List<Hitbox>();
            hitboxes.Add(hitbox);
            yield return CoroutineManager.Instance.StartCoroutine(ReleaseHitboxes(user, hitboxes, hitboxEffect, tileEffect));
        }
        public IEnumerator<YieldInstruction> ReleaseHitboxes(Character user, List<Hitbox> hitboxes, HitboxEffect hitboxEffect, HitboxEffect tileEffect)
        {
            yield return CoroutineManager.Instance.StartCoroutine(ReleaseHitboxes(user, hitboxes, Hitboxes, hitboxEffect, tileEffect));
        }

        public IEnumerator<YieldInstruction> ReleaseHitboxes(Character user, List<Hitbox> hitboxes, List<Hitbox> hitboxTo, HitboxEffect hitboxEffect, HitboxEffect tileEffect)
        {
            foreach (Hitbox hitbox in hitboxes)
            {
                //have all hitboxes pre-calculate their targets
                hitbox.PreCalculateAllTargets();
                hitbox.PreCalculateTileEmitters();
                //add the hitboxes to the screen
                hitboxTo.Add(hitbox);
            }

            yield return new WaitForFrames(GameManager.Instance.ModifyBattleSpeed(20, user.CharLoc, Settings.BattleSpeed.Slow));

            //set the NextStep to update the hit queue
            //this means that the hit queue will be checked at every frame if all events are clear
            //meanwhile, the update function can continue counting total time to keep subsequent hits consistent
            yield return CoroutineManager.Instance.StartCoroutine(ProcessHitQueue(hitboxes, hitboxEffect, tileEffect));
        }

        public IEnumerator<YieldInstruction> ProcessHitQueue(List<Hitbox> hitboxes, HitboxEffect burstEffect, HitboxEffect tileEffect)
        {
            //set the NextStep to update the hit queue; aka call this method again on next (available) update
            //stop doing it only if, for all hitboxes, everything has been hit and it's "done"
            //assume none of the hitboxes are blocking
            while (true)
            {
                StablePriorityQueue<int, HitboxHit> hitTargets = new StablePriorityQueue<int, HitboxHit>();

                foreach (Hitbox hitbox in hitboxes)
                    hitbox.UpdateHitQueue(hitTargets);

                //hit each target
                while (hitTargets.Count > 0)
                {
                    HitboxHit tile = hitTargets.Dequeue();
                    if (tile.Explode)
                        yield return CoroutineManager.Instance.StartCoroutine(burstEffect(tile.Loc));
                    else
                        yield return CoroutineManager.Instance.StartCoroutine(tileEffect(tile.Loc));
                }

                bool allDone = true;
                foreach (Hitbox hitbox in hitboxes)
                {
                    if (!hitbox.ProcessesDone())
                    {
                        allDone = false;
                        break;
                    }
                }
                if (allDone)
                    yield break;

                yield return new WaitForFrames(1);
            }
        }



        public delegate void EventEnqueueFunction<T>(StablePriorityQueue<GameEventPriority, Tuple<GameEventOwner, Character, T>> queue, int maxPriority, ref int nextPriority) where T : GameEvent;
        public static IEnumerable<Tuple<GameEventOwner, Character, T>> IterateEvents<T>(EventEnqueueFunction<T> enqueueFunction) where T : GameEvent
        {
            int maxPriority = Int32.MinValue;
            while (true)
            {
                int nextPriority = Int32.MaxValue;

                StablePriorityQueue<GameEventPriority, Tuple<GameEventOwner, Character, T>> queue = new StablePriorityQueue<GameEventPriority, Tuple<GameEventOwner, Character, T>>();

                enqueueFunction(queue, maxPriority, ref nextPriority);

                if (queue.Count == 0)
                    break;
                else
                {
                    while (queue.Count > 0)
                    {
                        Tuple<GameEventOwner, Character, T> effect = queue.Dequeue();
                        yield return effect;
                    }
                    if (nextPriority == Int32.MaxValue)
                        break;
                    else
                        maxPriority = nextPriority + 1;
                }
            }
        }


        public Alignment GetMatchup(Character attacker, Character target)
        {
            return GetMatchup(attacker, target, true);
        }

        public Alignment GetMatchup(Character attacker, Character target, bool action)
        {
            if (attacker == null) return Alignment.Foe;
            if (target == null) return Alignment.Foe;
            if (attacker == target)
                return Alignment.Self;
            if (attacker.MemberTeam == target.MemberTeam)
                return (target.EnemyOfFriend && action) ? Alignment.Foe : Alignment.Friend;
            else if (attacker.MemberTeam is MonsterTeam && target.MemberTeam is MonsterTeam)
                return (target.EnemyOfFriend && action) ? Alignment.Foe : Alignment.Friend;
            else
                return Alignment.Foe;
        }

        public bool IsTargeted(Character attacker, Character target, Alignment acceptedTargets)
        {
            if (attacker == null || target == null)
                return true;
            if (target.Dead)
                return false;
            Alignment alignment = GetMatchup(attacker, target);
            return (acceptedTargets & alignment) != 0;
        }

        public bool IsTargeted(Loc tile, TileAlignment tileAlignment)
        {
            if (!Collision.InBounds(ZoneManager.Instance.CurrentMap.Width, ZoneManager.Instance.CurrentMap.Height, tile))
                return false;

            if (tileAlignment == TileAlignment.None)
                return false;
            if (tileAlignment == TileAlignment.Any)
                return true;

            uint mobility = 0;
            mobility |= (1U << (int)TerrainData.Mobility.Lava);
            mobility |= (1U << (int)TerrainData.Mobility.Water);
            mobility |= (1U << (int)TerrainData.Mobility.Abyss);
            if (ZoneManager.Instance.CurrentMap.TileBlocked(tile, mobility))
                return true;
            else
                return false;
        }
    }
}

