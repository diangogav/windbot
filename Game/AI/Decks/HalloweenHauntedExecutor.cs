using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Haunted-house control bot for the Edison format: ghosts, cursed dolls
    // and reapers. The deck sets flip monsters (Castle of Dark Illusions,
    // Reaper of the Cards, Mask of Darkness, Ghost Gardna) to stall, locks the
    // opponent's best attacker down with Nightmare Wheel / Spellbinding Circle
    // / Shadow Spell, and beats with the doll beaters and the Level 6 ghosts
    // (Pumpking the King of Ghosts grows while the Castle is face-up, Ryu
    // Kokki hits hard). Dark Dust Spirit wipes every other face-up monster
    // and returns to the hand, so it can be replayed whenever the opponent's
    // board is worth more than ours; Puppet Master pays 2000 LP to raise two
    // Fiends from the graveyard. Michizure answers each lost monster with a
    // destruction, and Mask of Darkness recycles the best spent trap.
    // Deck: AI_HalloweenHaunted.ydk (Edison-whitelist-validated).
    [Deck("HalloweenHaunted", "AI_HalloweenHaunted")]
    public class HalloweenHauntedExecutor : DefaultExecutor
    {
        public class CardId
        {
            // Ghosts and reapers.
            public const int PumpkingTheKingOfGhosts = 29155212;
            public const int CastleOfDarkIllusions = 62121;
            public const int RyuKokki = 57281778;
            public const int DarkDustSpirit = 89111398;
            public const int SpiritReaper = 23205979;
            public const int ReaperOfTheCards = 33066139;
            public const int GhostGardna = 59965151;

            // Cursed dolls and puppets.
            public const int PuppetMaster = 41442341;
            public const int MaliceDollOfDemise = 72657739;
            public const int RogueDoll = 91939608;
            public const int MaskOfDarkness = 28933734;
            public const int DarkJeroid = 90980792;

            // Spells not present in the shared _CardId class.
            public const int Fissure = 66788016;
            public const int SwordsOfRevealingLight = 72302403;

            // Traps not present in the shared _CardId class.
            public const int NightmareWheel = 54704216;
            public const int SpellbindingCircle = 18807108;
            public const int ShadowSpell = 29267084;
            public const int Michizure = 37580756;
            public const int DustTornado = 60082869;
            public const int MirrorForce = 44095762;
            public const int BottomlessTrapHole = 29401950;
            public const int MagicCylinder = 62279055;
        }

        // Continuous traps that lock one enemy monster; used to avoid stacking
        // two locks on the same target.
        private static readonly int[] LockTraps =
        {
            CardId.NightmareWheel,
            CardId.SpellbindingCircle,
            CardId.ShadowSpell
        };

        // Mask of Darkness recovery order: strongest one-shot answers first.
        private static readonly int[] TrapRecoveryPriority =
        {
            CardId.MirrorForce,
            _CardId.TorrentialTribute,
            CardId.MagicCylinder,
            CardId.BottomlessTrapHole,
            _CardId.CallOfTheHaunted,
            CardId.NightmareWheel,
            CardId.Michizure,
            CardId.SpellbindingCircle,
            CardId.ShadowSpell,
            CardId.DustTornado
        };

        public HalloweenHauntedExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // 1. Backrow removal first, so locks and summons land on a clean field.
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultEndPhaseSpellTrapRemoval);
            AddExecutor(ExecutorType.Activate, CardId.DustTornado, DefaultEndPhaseSpellTrapRemoval);

            // 2. Monster removal and stall spells.
            AddExecutor(ExecutorType.Activate, CardId.Fissure, FissureEffect);
            AddExecutor(ExecutorType.Activate, _CardId.SmashingGround, DefaultSmashingGround);
            AddExecutor(ExecutorType.Activate, CardId.SwordsOfRevealingLight, SwordsOfRevealingLightEffect);

            // 3. Reactive traps.
            AddExecutor(ExecutorType.Activate, CardId.MirrorForce);
            AddExecutor(ExecutorType.Activate, CardId.MagicCylinder, MagicCylinderEffect);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, CardId.BottomlessTrapHole, DefaultUniqueTrap);
            AddExecutor(ExecutorType.Activate, CardId.Michizure, MichizureEffect);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, CallOfTheHauntedEffect);

            // 4. Lock traps: pin down the opponent's best attacker.
            AddExecutor(ExecutorType.Activate, CardId.NightmareWheel, LockTrapEffect);
            AddExecutor(ExecutorType.Activate, CardId.SpellbindingCircle, LockTrapEffect);
            AddExecutor(ExecutorType.Activate, CardId.ShadowSpell, ShadowSpellEffect);

            // 5. Monster effects that need a decision.
            AddExecutor(ExecutorType.Activate, CardId.PuppetMaster, PuppetMasterEffect);
            AddExecutor(ExecutorType.Activate, CardId.GhostGardna, GhostGardnaEffect);
            AddExecutor(ExecutorType.Activate, CardId.ReaperOfTheCards, ReaperOfTheCardsEffect);
            AddExecutor(ExecutorType.Activate, CardId.MaskOfDarkness, MaskOfDarknessEffect);
            AddExecutor(ExecutorType.Activate, CardId.DarkJeroid, DarkJeroidEffect);

            // 6. Normal Summon priority: the board wipe, then the Fiend revival,
            // then the big ghosts, then the debuffer and the doll beaters.
            AddExecutor(ExecutorType.Summon, CardId.DarkDustSpirit, DarkDustSpiritSummon);
            AddExecutor(ExecutorType.Summon, CardId.PuppetMaster, PuppetMasterSummon);
            AddExecutor(ExecutorType.Summon, CardId.RyuKokki, TributeSummonOneMonster);
            AddExecutor(ExecutorType.Summon, CardId.PumpkingTheKingOfGhosts, TributeSummonOneMonster);
            AddExecutor(ExecutorType.Summon, CardId.DarkJeroid, DarkJeroidSummon);
            AddExecutor(ExecutorType.Summon, CardId.SpiritReaper, SpiritReaperSummon);

            // 7. Flip monsters are always Set for their FLIP value.
            AddExecutor(ExecutorType.MonsterSet, CardId.ReaperOfTheCards);
            AddExecutor(ExecutorType.MonsterSet, CardId.MaskOfDarkness);
            AddExecutor(ExecutorType.MonsterSet, CardId.CastleOfDarkIllusions);
            AddExecutor(ExecutorType.MonsterSet, CardId.GhostGardna);
            AddExecutor(ExecutorType.MonsterSet, CardId.SpiritReaper);

            // 8. Flip Summons / position changes with card-specific timing.
            AddExecutor(ExecutorType.Repos, CardId.CastleOfDarkIllusions, CastleOfDarkIllusionsRepos);
            AddExecutor(ExecutorType.Repos, CardId.ReaperOfTheCards, ReaperOfTheCardsRepos);
            AddExecutor(ExecutorType.Repos, CardId.MaskOfDarkness, MaskOfDarknessRepos);
            AddExecutor(ExecutorType.Repos, CardId.GhostGardna, StayInDefenseRepos);
            AddExecutor(ExecutorType.Repos, CardId.SpiritReaper, SpiritReaperRepos);

            // 9. Generic fallback play: doll beaters, backrow, reposition.
            AddExecutor(ExecutorType.SummonOrSet, CardId.MaliceDollOfDemise, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SummonOrSet, CardId.RogueDoll, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        // Fissure hits the lowest-ATK face-up monster, so it is only worth it
        // while the opponent actually has a face-up monster to lose.
        private bool FissureEffect()
        {
            return Enemy.GetMonsters().Any(card => card.IsFaceup());
        }

        // Swords of Revealing Light buys three turns: use it when the opponent
        // has a monster we cannot beat, or face-down monsters to expose.
        private bool SwordsOfRevealingLightEffect()
        {
            if (Bot.HasInSpellZone(CardId.SwordsOfRevealingLight, true, true))
                return false;

            return Util.IsOneEnemyBetter(true) || Enemy.GetMonsters().Any(card => card.IsFacedown());
        }

        // Magic Cylinder: only bounce a hit that is worth the card.
        private bool MagicCylinderEffect()
        {
            ClientCard attacker = Enemy.BattlingMonster;
            return attacker != null && attacker.Attack >= 1500;
        }

        // Michizure: whenever we lose a monster, take the opponent's best
        // monster with it. Never fire it when the only legal target is ours.
        private bool MichizureEffect()
        {
            ClientCard target = Util.GetBestEnemyMonster(false, true);
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Call of the Haunted: revive our strongest graveyard monster only when
        // the opponent's board is ahead. Dark Dust Spirit cannot be Special
        // Summoned, so it is never the target.
        private bool CallOfTheHauntedEffect()
        {
            if (!Util.IsAllEnemyBetter(true))
                return false;

            ClientCard target = Bot.Graveyard
                .Where(card => card.IsMonster() && card.IsCanRevive() && !card.IsCode(CardId.DarkDustSpirit))
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Nightmare Wheel / Spellbinding Circle: lock the opponent's biggest
        // attacker during their turn, once it outclasses our best monster.
        private bool LockTrapEffect()
        {
            if (Duel.Player != 1)
                return false;

            ClientCard target = GetLockTarget(true);
            if (target == null)
                return false;

            AI.SelectCard(target);
            return true;
        }

        // Shadow Spell: same lock on the opponent's turn; on our turn the
        // -700 ATK can also turn an even fight into a kill for our best attacker.
        private bool ShadowSpellEffect()
        {
            ClientCard target = GetLockTarget(Duel.Player == 1);
            if (target == null)
                return false;

            if (Duel.Player == 0)
            {
                if (Duel.Phase != DuelPhase.Main1 || !target.IsAttack())
                    return false;

                int ourBest = Util.GetBestAttack(Bot);
                if (ourBest > target.Attack || ourBest <= target.Attack - 700)
                    return false;
            }

            AI.SelectCard(target);
            return true;
        }

        // Face-down monsters cannot attack until flipped, so only face-up
        // attackers that outclass our best monster (or any attacker while our
        // side is empty) are worth a lock.
        private ClientCard GetLockTarget(bool requireThreat)
        {
            int ourBest = Util.GetBestPower(Bot);
            bool weHaveBoard = Bot.GetMonsters().Any(card => card.IsFaceup());

            return Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && !IsLocked(card))
                .Where(card => !requireThreat || card.Attack >= ourBest || !weHaveBoard)
                .OrderByDescending(card => card.Attack)
                .FirstOrDefault();
        }

        private bool IsLocked(ClientCard monster)
        {
            return Bot.GetSpells().Any(spell =>
                spell.IsFaceup() && spell.IsCode(LockTraps) && spell.OwnTargets.Contains(monster));
        }

        // Puppet Master: pay 2000 LP to raise our two best Fiends. The summon
        // condition already checked that the targets exist and the LP is safe.
        private bool PuppetMasterEffect()
        {
            if (Bot.LifePoints <= 3000)
                return false;

            List<ClientCard> fiends = GetRevivableFiends();
            if (fiends.Count < 2)
                return false;

            AI.SelectCard(fiends.Take(2).ToList());
            return true;
        }

        private List<ClientCard> GetRevivableFiends()
        {
            return Bot.Graveyard
                .Where(card => card.IsMonster() && card.HasRace(CardRace.Fiend) && card.IsCanRevive())
                .OrderByDescending(card => card.Attack)
                .ToList();
        }

        // Ghost Gardna (attack redirect): pull an attack onto this card when it
        // survives the hit in Defense Position, or when the original target
        // would die anyway (Gardna's own death then shrinks the attacker).
        private bool GhostGardnaEffect()
        {
            if (ActivateDescription != Util.GetStringId(CardId.GhostGardna, 0))
                return true; // forced death trigger; target picked in OnSelectCard.

            ClientCard attacker = Enemy.BattlingMonster;
            ClientCard target = Bot.BattlingMonster;
            if (attacker == null || target == null || target == Card || !Card.IsFaceup())
                return false;

            if (Card.IsDefense() && Card.Defense > attacker.Attack)
                return true;

            return target.GetDefensePower() <= attacker.Attack && target.Attack > 0;
        }

        // Reaper of the Cards: its FLIP destroys a Trap; pick the opponent's
        // face-up trap first, then their face-down cards.
        private bool ReaperOfTheCardsEffect()
        {
            ClientCard target = GetReaperTarget(Enemy.GetSpells().Concat(Bot.GetSpells()).ToList());
            if (target != null)
                AI.SelectCard(target);
            return true;
        }

        // Mask of Darkness: recover the strongest trap from our graveyard.
        private bool MaskOfDarknessEffect()
        {
            ClientCard target = GetBestTrapToRecover(Bot.Graveyard.ToList());
            if (target != null)
                AI.SelectCard(target);
            return true;
        }

        // Dark Jeroid: shrink the opponent's strongest face-up monster.
        private bool DarkJeroidEffect()
        {
            ClientCard target = Enemy.GetMonsters().Where(card => card.IsFaceup()).GetHighestAttackMonster();
            if (target != null)
                AI.SelectCard(target);
            return true;
        }

        // Dark Dust Spirit: tribute 1 and destroy every other face-up monster.
        // Only worth it while the opponent's face-up board outweighs ours; the
        // Spirit returns to the hand in the End Phase, ready to be replayed.
        private bool DarkDustSpiritSummon()
        {
            List<ClientCard> enemyFaceup = Enemy.GetMonsters().Where(card => card.IsFaceup()).ToList();
            if (enemyFaceup.Count == 0)
                return false;

            ClientCard tribute = GetTribute(int.MaxValue, true);
            if (tribute == null)
                return false;

            int ourLoss = Bot.GetMonsters()
                .Where(card => card.IsFaceup() && card != tribute)
                .Sum(card => card.Attack);
            int enemyLoss = enemyFaceup.Sum(card => card.Attack);
            if (enemyLoss <= ourLoss)
                return false;

            return true;
        }

        // Puppet Master needs 2 free zones for its revival, 2 Fiends in the
        // graveyard worth the 2000 LP, and a cheap tribute.
        private bool PuppetMasterSummon()
        {
            if (Bot.LifePoints <= 3000 || Bot.GetMonsterCount() > 3)
                return false;

            List<ClientCard> fiends = GetRevivableFiends();
            if (fiends.Count < 2 || fiends[0].Attack + fiends[1].Attack < 2800)
                return false;

            ClientCard tribute = GetTribute(2000, false);
            if (tribute == null)
                return false;

            return true;
        }

        // Level 5-6 ghosts: tribute 1 monster weaker than the one coming down.
        private bool TributeSummonOneMonster()
        {
            ClientCard tribute = GetTribute(Card.Attack, false);
            if (tribute == null)
                return false;

            return true;
        }

        // Pick the cheapest tribute below the given power. Flip monsters still
        // waiting to be flipped are kept, and face-up monsters can be preferred
        // when the summon would destroy them anyway (Dark Dust Spirit).
        private ClientCard GetTribute(int maxPower, bool preferFaceup)
        {
            return Bot.GetMonsters()
                .Where(card => card.GetDefensePower() < maxPower)
                .OrderBy(card => preferFaceup && card.IsFaceup() ? 0 : 1)
                .ThenBy(card => IsUnflippedFlipMonster(card) ? 1 : 0)
                .ThenBy(card => card.GetDefensePower())
                .FirstOrDefault();
        }

        private bool IsUnflippedFlipMonster(ClientCard card)
        {
            return card.IsFacedown() && card.IsCode(CardId.CastleOfDarkIllusions, CardId.ReaperOfTheCards, CardId.MaskOfDarkness);
        }

        // Dark Jeroid's trigger is forced and can hit our own monsters, so
        // only summon it when the opponent has a face-up monster to shrink.
        private bool DarkJeroidSummon()
        {
            return Enemy.GetMonsters().Any(card => card.IsFaceup());
        }

        // Spirit Reaper attacks face-up only into an empty board (direct hit
        // plus a random discard); otherwise it is Set as an unkillable wall.
        private bool SpiritReaperSummon()
        {
            return Enemy.GetMonsterCount() == 0;
        }

        private bool SpiritReaperRepos()
        {
            bool wantAttack = Enemy.GetMonsterCount() == 0 && Duel.Phase == DuelPhase.Main1;
            if (Card.IsFacedown())
                return wantAttack;
            return Card.IsAttack() != wantAttack;
        }

        // Castle of Dark Illusions: flip it once a Zombie on our side can use
        // the ATK/DEF boost (Pumpking also gains from the Castle being face-up);
        // keep it in Defense Position afterwards.
        private bool CastleOfDarkIllusionsRepos()
        {
            if (Card.IsFacedown())
                return Bot.GetMonsters().Any(card => card != Card && card.IsFaceup() && card.HasRace(CardRace.Zombie));
            return Card.IsAttack();
        }

        // Reaper of the Cards: flip it while the opponent has backrow to hit.
        private bool ReaperOfTheCardsRepos()
        {
            if (Card.IsFacedown())
                return Enemy.GetSpells().Any(card => card.IsFacedown() || card.IsTrap());
            return Card.IsAttack() && Util.IsOneEnemyBetter();
        }

        // Mask of Darkness: flip it once there is a trap worth recovering.
        private bool MaskOfDarknessRepos()
        {
            if (Card.IsFacedown())
                return GetBestTrapToRecover(Bot.Graveyard.ToList()) != null;
            return Card.IsAttack();
        }

        private bool StayInDefenseRepos()
        {
            return Card.IsFaceup() && Card.IsAttack();
        }

        private static ClientCard GetBestTrapToRecover(IList<ClientCard> cards)
        {
            foreach (int id in TrapRecoveryPriority)
            {
                ClientCard trap = cards.FirstOrDefault(card => card.IsCode(id));
                if (trap != null)
                    return trap;
            }
            return cards.FirstOrDefault(card => card.IsTrap());
        }

        private static ClientCard GetReaperTarget(IList<ClientCard> cards)
        {
            return cards
                .OrderBy(card => ReaperTargetScore(card))
                .FirstOrDefault();
        }

        // Lower is better: the opponent's face-up traps, then their face-down
        // cards, then our own non-trap cards (only revealed, never destroyed),
        // and our own traps last.
        private static int ReaperTargetScore(ClientCard card)
        {
            if (card.Controller == 1)
                return card.IsFaceup() ? 0 : 1;
            return card.IsTrap() ? 3 : 2;
        }

        // Forced FLIP / trigger targets that the engine asks for without an
        // Activate prompt (Reaper of the Cards, Mask of Darkness, Dark Jeroid,
        // Ghost Gardna's death trigger) still get the deliberate choice.
        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (!AI.HaveSelectedCards() && min == 1 && cards.Count >= 1)
            {
                if (hint == HintMsg.Destroy && cards.All(card => card.Location == CardLocation.SpellZone))
                    return new List<ClientCard> { GetReaperTarget(cards) };

                if (hint == HintMsg.AddToHand && cards.All(card => card.IsTrap() && card.Location == CardLocation.Grave))
                    return new List<ClientCard> { GetBestTrapToRecover(cards) };

                if (hint == HintMsg.Faceup && cards.All(card => card.Location == CardLocation.MonsterZone))
                {
                    ClientCard enemyBest = cards.Where(card => card.Controller == 1).GetHighestAttackMonster();
                    ClientCard ourWorst = cards.Where(card => card.Controller == 0).OrderBy(card => card.Attack).FirstOrDefault();
                    return new List<ClientCard> { enemyBest ?? ourWorst };
                }
            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }
    }
}
