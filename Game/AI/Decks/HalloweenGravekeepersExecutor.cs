using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Gravekeeper's beatdown bot for the Edison format (TCG April 2010 card
    // pool), built for the Halloween event. Necrovalley (searched by
    // Gravekeeper's Commandant) gives every Gravekeeper +500 ATK/DEF and turns
    // on Gravekeeper's Assailant's position change and Royal Tribute; Gravekeeper's
    // Priestess stands in for Necrovalley while no field spell is up. Set
    // Gravekeeper's Spy floats into another Gravekeeper from the Deck, Gravekeeper's
    // Guard bounces the opponent's best monster, Gravekeeper's Descendant trades a
    // spent Gravekeeper for any card on the opponent's field, and the Chief /
    // Visionary pair turns those bodies into big Tribute Summons. Gravekeeper's
    // Stele refills the hand (it ignores Necrovalley), Cannonholder closes out
    // games and a staple trap line protects the board.
    // Necrovalley uses the PRE-ERRATA passcode 910003011 required by the Edison
    // whitelist (alias of the official 47355498); it is defined locally and
    // the official id is only used to recognise an opponent's copy.
    // Deck: AI_HalloweenGravekeepers.ydk (Edison-whitelist-validated).
    [Deck("HalloweenGravekeepers", "AI_HalloweenGravekeepers")]
    public class HalloweenGravekeepersExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Gravekeeper's monsters.
            public const int Spy = 24317029;
            public const int Guard = 37101832;
            public const int Descendant = 30213599;
            public const int Commandant = 17393207;
            public const int Assailant = 25262697;
            public const int SpearSoldier = 63695531;
            public const int Chief = 62473983;
            public const int Visionary = 3825890;
            public const int Cannonholder = 99877698;
            public const int Priestess = 3381441;
            public const int Curse = 50712728;

            // Gravekeeper's spells.
            public const int Necrovalley = 910003011; // PRE-ERRATA passcode (Edison whitelist).
            public const int NecrovalleyOfficial = 47355498;
            public const int RoyalTribute = 72405967;
            public const int Stele = 99523325;

            // Staples not present in the shared _CardId class.
            public const int BrainControl = 511002995; // PRE-ERRATA passcode (Edison whitelist).
            public const int SwordsOfRevealingLight = 72302403;
            public const int MirrorForce = 44095762;
            public const int BottomlessTrapHole = 29401950;
            public const int DimensionalPrison = 70342110;
            public const int MagicCylinder = 62279055;
            public const int SakuretsuArmor = 56120475;
        }

        // Every Gravekeeper's monster in this list, used to tell archetype bodies
        // apart from anything stolen with Brain Control.
        private static readonly int[] Gravekeepers =
        {
            CardId.Spy, CardId.Guard, CardId.Descendant, CardId.Commandant, CardId.Assailant,
            CardId.SpearSoldier, CardId.Chief, CardId.Visionary, CardId.Cannonholder,
            CardId.Priestess, CardId.Curse
        };

        public HalloweenGravekeepersExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Field setup: Necrovalley first, Commandant fetches it when missing.
            AddExecutor(ExecutorType.Activate, CardId.Necrovalley, NecrovalleyEffect);
            AddExecutor(ExecutorType.Activate, CardId.Commandant, CommandantEffect);

            // Removal and disruption staples with DefaultExecutor's smart timing.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultEndPhaseSpellTrapRemoval);
            AddExecutor(ExecutorType.Activate, _CardId.SmashingGround, DefaultSmashingGround);
            AddExecutor(ExecutorType.Activate, _CardId.BookOfMoon, DefaultBookOfMoon);
            AddExecutor(ExecutorType.Activate, CardId.BrainControl, BrainControlEffect);
            AddExecutor(ExecutorType.Activate, CardId.SwordsOfRevealingLight, SwordsOfRevealingLightEffect);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce, AttackResponseTrapEffect);
            AddExecutor(ExecutorType.Activate, CardId.SakuretsuArmor, AttackResponseTrapEffect);
            AddExecutor(ExecutorType.Activate, CardId.MagicCylinder, MagicCylinderEffect);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.DimensionalPrison, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, _CardId.SolemnJudgment, DefaultSolemnJudgment);

            // Gravekeeper's monster effects.
            AddExecutor(ExecutorType.Activate, CardId.Descendant, DescendantEffect);
            AddExecutor(ExecutorType.Activate, CardId.Chief, ChiefEffect);
            AddExecutor(ExecutorType.Activate, CardId.Spy, SpyEffect);
            AddExecutor(ExecutorType.Activate, CardId.Guard, GuardEffect);
            AddExecutor(ExecutorType.Activate, CardId.Assailant, AssailantEffect);
            AddExecutor(ExecutorType.Activate, CardId.Visionary, VisionaryEffect);
            AddExecutor(ExecutorType.Activate, CardId.Cannonholder, CannonholderEffect);

            // Hand refills.
            AddExecutor(ExecutorType.Activate, CardId.Stele, SteleEffect);
            AddExecutor(ExecutorType.Activate, CardId.RoyalTribute, RoyalTributeEffect);

            // Normal Summon priority: big Tribute Summons first, then a
            // Descendant that can pop something right away, then the set floaters.
            AddExecutor(ExecutorType.Summon, CardId.Chief, ChiefSummon);
            AddExecutor(ExecutorType.Summon, CardId.Visionary, VisionarySummon);
            AddExecutor(ExecutorType.Summon, CardId.Descendant, DescendantSummon);
            AddExecutor(ExecutorType.Summon, CardId.Priestess, PriestessSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.Spy, SpySet);
            AddExecutor(ExecutorType.MonsterSet, CardId.Guard, GuardSet);
            AddExecutor(ExecutorType.Summon, CardId.Assailant, NecrovalleyBeaterSummon);
            AddExecutor(ExecutorType.Summon, CardId.SpearSoldier, NecrovalleyBeaterSummon);

            // Flip the set floaters at the right moment.
            AddExecutor(ExecutorType.Repos, CardId.Spy, SpyFlip);
            AddExecutor(ExecutorType.Repos, CardId.Guard, GuardFlip);

            // Generic fallback play: summon what's left, set backrow, reposition.
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        // Necrovalley is "active" for our cards when either player's field spell
        // is Necrovalley or a face-up Gravekeeper's Priestess changes the field.
        private bool NecrovalleyActive()
        {
            return Bot.HasInSpellZone(CardId.Necrovalley, true, true)
                || Bot.HasInSpellZone(CardId.NecrovalleyOfficial, true, true)
                || Enemy.HasInSpellZone(CardId.Necrovalley, true, true)
                || Enemy.HasInSpellZone(CardId.NecrovalleyOfficial, true, true)
                || Bot.HasInMonstersZone(CardId.Priestess, true, false, true)
                || Enemy.HasInMonstersZone(CardId.Priestess, true, false, true);
        }

        private bool IsGravekeeper(ClientCard card)
        {
            return card != null && card.IsCode(Gravekeepers);
        }

        private List<ClientCard> FaceupGravekeepers()
        {
            return Bot.GetMonsters().Where(card => card.IsFaceup() && IsGravekeeper(card)).ToList();
        }

        // Activate Necrovalley whenever our own field zone is empty; a second
        // copy is kept in hand to rebuild after removal.
        private bool NecrovalleyEffect()
        {
            return DefaultField();
        }

        // Commandant: discard itself to search Necrovalley, but only while we
        // have no copy in hand or on the field and one is still in the Deck.
        private bool CommandantEffect()
        {
            if (Card.Location != CardLocation.Hand)
                return false;
            if (Bot.HasInHandOrInSpellZone(CardId.Necrovalley))
                return false;
            return Bot.GetRemainingCount(CardId.Necrovalley, 3) > 0;
        }

        // Pre-errata Brain Control: pay 800 LP to borrow the opponent's best
        // face-up monster for the turn. In Main Phase 1 so it can attack or
        // become Tribute fodder for Chief / Visionary.
        private bool BrainControlEffect()
        {
            if (Duel.Phase != DuelPhase.Main1 || Bot.LifePoints <= 800)
                return false;

            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !card.IsShouldNotBeTarget())
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            if (target == null || target.Attack < 1500)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Swords of Revealing Light: stall only when the opponent's board
        // outclasses ours right now.
        private bool SwordsOfRevealingLightEffect()
        {
            return Enemy.GetMonsterCount() > 0 && Util.IsOneEnemyBetter(true);
        }

        // Mirror Force / Sakuretsu Armor: answer an opponent's attack, and only
        // when the attacker is a real threat to the monster it targets (or to
        // our Life Points).
        private bool AttackResponseTrapEffect()
        {
            if (Duel.Player != 1 || Enemy.BattlingMonster == null)
                return false;

            ClientCard defender = Bot.BattlingMonster;
            if (defender == null)
                return true;
            return Enemy.BattlingMonster.Attack >= defender.GetDefensePower();
        }

        // Magic Cylinder: bounce the attack back as burn when it is worth it.
        private bool MagicCylinderEffect()
        {
            return Duel.Player == 1 && Enemy.BattlingMonster != null && Enemy.BattlingMonster.Attack >= 1500;
        }

        // Descendant: Tribute the cheapest other face-up Gravekeeper to destroy
        // any card the opponent controls. The release cost is asked before the
        // target, so both picks are queued in that order. Only spend
        // the body when the target is worth more than the tribute: a monster
        // stronger than our tribute, or any Spell/Trap before we go to battle.
        private bool DescendantEffect()
        {
            List<ClientCard> fodder = FaceupGravekeepers().Where(card => card != Card).ToList();
            if (fodder.Count == 0)
                return false;
            ClientCard cheapestFodder = fodder.OrderBy(card => card.Attack).First();
            int cheapest = cheapestFodder.Attack;

            ClientCard monster = Util.GetBestEnemyMonster(false, true);
            if (monster != null && (monster.IsFacedown() || monster.GetDefensePower() >= cheapest))
            {
                AI.SelectCard(cheapestFodder);
                AI.SelectNextCard(monster);
                return true;
            }

            ClientCard spell = Util.GetBestEnemySpell();
            if (spell != null && Duel.Phase == DuelPhase.Main1)
            {
                AI.SelectCard(cheapestFodder);
                AI.SelectNextCard(spell);
                return true;
            }
            return false;
        }

        // Chief's Tribute Summon trigger: revive the best Gravekeeper from the
        // Graveyard (Chief keeps our Graveyard immune to Necrovalley).
        private bool ChiefEffect()
        {
            ClientCard target = Bot.Graveyard
                .Where(card => IsGravekeeper(card) && !card.IsCode(CardId.Chief))
                .OrderByDescending(SummonScore)
                .FirstOrDefault();
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Spy's FLIP Special Summon from the Deck; the body itself is chosen by
        // SummonScore in OnSelectCard.
        private bool SpyEffect()
        {
            return true;
        }

        // Guard's FLIP bounce; the target (the opponent's best monster) is
        // chosen in OnSelectCard.
        private bool GuardEffect()
        {
            return true;
        }

        // Assailant (while Necrovalley is active): when it declares an attack,
        // flip the battle target's position when that turns a losing or
        // pointless fight into a kill — an Attack Position wall whose DEF is
        // lower than our ATK, or a Defense Position monster with low ATK so the
        // hit also deals damage.
        private bool AssailantEffect()
        {
            ClientCard target = Enemy.BattlingMonster;
            if (target == null || target.IsFacedown())
                return false;

            bool breakWall = target.IsAttack() && target.Attack >= Card.Attack && target.Defense < Card.Attack;
            bool openDamage = target.IsDefense() && target.Attack < Card.Attack;
            if (!breakWall && !openDamage)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Visionary's destruction replacement: send a Gravekeeper from hand
        // instead (the cheapest one is picked in OnSelectCard). Visionary is
        // our biggest body, always worth protecting.
        private bool VisionaryEffect()
        {
            return Card.Location == CardLocation.MonsterZone;
        }

        // Cannonholder: Tribute a non-Cannonholder Gravekeeper for 700 damage —
        // only to close out the game, or in Main Phase 2 to cash in a spent
        // flip monster that has nothing else to do.
        private bool CannonholderEffect()
        {
            List<ClientCard> fodder = FaceupGravekeepers()
                .Where(card => !card.IsCode(CardId.Cannonholder))
                .OrderBy(card => IsSpentFlipMonster(card) ? 0 : 1)
                .ThenBy(card => card.Attack)
                .ToList();
            if (fodder.Count == 0)
                return false;

            bool lethal = Enemy.LifePoints <= 700;
            if (!lethal && (Duel.Phase != DuelPhase.Main2 || !IsSpentFlipMonster(fodder[0])))
                return false;

            AI.SelectCard(fodder[0]);
            return true;
        }

        private bool IsSpentFlipMonster(ClientCard card)
        {
            return card.IsCode(CardId.Spy) || card.IsCode(CardId.Guard) || card.IsCode(CardId.Curse);
        }

        // Stele: add 2 Gravekeepers from the Graveyard to hand (it ignores
        // Necrovalley). Pick the two best bodies.
        private bool SteleEffect()
        {
            List<ClientCard> targets = Bot.Graveyard
                .Where(IsGravekeeper)
                .OrderByDescending(SummonScore)
                .Take(2)
                .ToList();
            if (targets.Count < 2)
                return false;

            AI.SelectCard(targets);
            return true;
        }

        // Royal Tribute: both players discard every monster in hand. Only when
        // we lose little (at most 1 monster in hand) and the opponent still holds
        // a real hand.
        private bool RoyalTributeEffect()
        {
            if (!Bot.HasInSpellZone(CardId.Necrovalley, true, true) && !Bot.HasInMonstersZone(CardId.Priestess, true, false, true))
                return false;

            int ourMonsters = Bot.Hand.Count(card => card.IsMonster());
            return ourMonsters <= 1 && Enemy.GetHandCount() >= 3;
        }

        // Chief: Tribute Summon when a Gravekeeper waits in the Graveyard and we
        // have a weaker monster to tribute, after any pending FLIP Summon.
        private bool ChiefSummon()
        {
            if (!Bot.Graveyard.Any(card => IsGravekeeper(card) && !card.IsCode(CardId.Chief)))
                return false;
            if (HasFlipSummonPending())
                return false;
            ClientCard worst = Util.GetWorstBotMonster();
            return worst != null && worst.GetDefensePower() < 1900;
        }

        // Visionary can be Tribute Summoned with a single Gravekeeper (face-up or
        // set) we control; it gains 200 ATK per Gravekeeper in our Graveyard.
        // A set Spy/Guard is flipped first so its FLIP resolves before the tribute.
        private bool VisionarySummon()
        {
            return Bot.GetMonsters().Any(IsGravekeeper) && !HasFlipSummonPending();
        }

        // Descendant: summon it face-up when another face-up Gravekeeper is
        // already there to feed its destroy effect and the opponent has a card.
        private bool DescendantSummon()
        {
            return FaceupGravekeepers().Count > 0 && (Enemy.GetMonsterCount() + Enemy.GetSpellCount()) > 0;
        }

        // Priestess turns the field into Necrovalley on her own; worth a summon
        // while we have no Necrovalley up.
        private bool PriestessSummon()
        {
            return !NecrovalleyActive();
        }

        // Spy is always set so its FLIP floats into another Gravekeeper.
        private bool SpySet()
        {
            return Bot.GetMonsterCount() < 5;
        }

        // Guard is set when the opponent has a monster worth bouncing, or as a
        // defensive body on an empty board.
        private bool GuardSet()
        {
            return Enemy.GetMonsterCount() > 0 || Bot.GetMonsterCount() == 0;
        }

        // Assailant / Spear Soldier are plain beaters: summon them face-up while
        // Necrovalley pumps them, otherwise leave them to the fallback.
        private bool NecrovalleyBeaterSummon()
        {
            return NecrovalleyActive();
        }

        // Flip-summon Spy whenever a Monster Zone is free for its float.
        private bool SpyFlip()
        {
            return Card.IsFacedown() && Bot.GetMonsterCount() < 5;
        }

        // Flip-summon Guard only with a target to bounce.
        private bool GuardFlip()
        {
            return Card.IsFacedown() && Enemy.GetMonsterCount() > 0;
        }

        // How good a Gravekeeper is as a summon/revive/add-to-hand target.
        private int SummonScore(ClientCard card)
        {
            if (card.IsCode(CardId.Visionary))
                return 100;
            if (card.IsCode(CardId.Descendant))
                return 90;
            if (card.IsCode(CardId.Priestess))
                return NecrovalleyActive() ? 50 : 85;
            if (card.IsCode(CardId.SpearSoldier))
                return 70;
            if (card.IsCode(CardId.Assailant))
                return NecrovalleyActive() ? 75 : 60;
            if (card.IsCode(CardId.Spy))
                return 65;
            if (card.IsCode(CardId.Chief))
                return 60;
            if (card.IsCode(CardId.Cannonholder))
                return 55;
            if (card.IsCode(CardId.Commandant))
                return 50;
            if (card.IsCode(CardId.Guard))
                return 45;
            if (card.IsCode(CardId.Curse))
                return 40;
            return 0;
        }

        // Card-selection smarts per hint; an explicit queued selection always
        // takes precedence.
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (!AI.HaveSelectedCards())
            {
                // Spy's FLIP float and Chief's revive: best Gravekeeper first.
                if (hint == HintMsg.SpSummon && cards.All(card => card.Controller == 0))
                {
                    return cards.OrderByDescending(SummonScore).Take(min).ToList();
                }

                // Visionary's replacement discard: give up the least useful one.
                if ((hint == HintMsg.Discard || hint == HintMsg.ToGrave) && cards.All(card => card.Location == CardLocation.Hand))
                {
                    return cards.OrderBy(SummonScore).Take(min).ToList();
                }

                // Guard's bounce and any untargeted destroy: hit the opponent's
                // strongest card, never our own.
                if (hint == HintMsg.ReturnToHand || hint == HintMsg.Destroy)
                {
                    List<ClientCard> enemy = cards
                        .Where(card => card.Controller == 1)
                        .OrderByDescending(card => card.IsMonster() ? card.GetDefensePower() : 0)
                        .ToList();
                    IList<ClientCard> picked = Util.CheckSelectCount(enemy, cards, min, max);
                    if (enemy.Count > 0 && picked != null)
                        return picked;
                }
            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }
    }
}
