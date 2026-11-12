using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    // Genesys Labrynth list: the Labrynth core of LabrynthExecutor with a trap toolbox
    // (Trap Trick, Dogmatika Punishment, Simultaneous Equation Cannons, Terrors of the Overroot)
    // and an Extra Deck that only exists to be banished or sent by those traps.
    [Deck("LabrynthGenesys", "AI_LabrynthGenesys")]
    public class LabrynthGenesysExecutor : LabrynthExecutor
    {
        public new class CardId : LabrynthExecutor.CardId
        {
            public const int AbsoluteKingBackJack = 60990740;
            public const int DogmatikaFleurdelis = 73355772;

            public const int LabrynthLabyrinth = 33407125;
            public const int LabrynthSetUp = 69895264;

            public const int TrapTrick = 80101899;
            public const int TerrorsOfTheOverroot = 63086455;
            public const int DogmatikaPunishment = 82956214;
            public const int SimultaneousEquationCannons = 25096909;

            public const int EnigmasterPackbit = 72444406;
            public const int TitanikladTheAshDragon = 41373230;
            public const int FossilMachineSkullWagon = 83656563;
            public const int FrekiTheRunickFangs = 47219274;
            public const int GeriTheRunickFangs = 28373620;
            public const int HuginTheRunickWings = 55990317;
            public const int VallonTheSuperPsySkyblaster = 40673853;
            public const int EvilswarmExcitonKnight = 46772449;
            public const int SuperQuantalMechBeastGrampulse = 85252081;
            public const int Number20GigaBrilliant = 47805931;
            public const int Number49FortuneTune = 16259549;
            public const int GhostrickSocuteboss = 32224143;
            public const int Number65DjinnBuster = 3790062;
        }

        private class CannonPlan
        {
            public List<ClientCard> Banish;
            public ClientCard ReturnXyz;
            public ClientCard ReturnFusion;
            public ClientCard Target;
            public int Score;
        }

        // System strings "Special Summon" / "Add to hand" used by Titaniklad's choice.
        private const int OptionAddToHand = 1190;
        private const int OptionSpecialSummon = 1152;
        private const int WorthRemovingValue = 25;
        private static readonly int[] YubelForms = { 78371393, 4779091, 31764700, 90829280, 80453041, 47172959 };

        private CannonPlan cannonPlan;
        private bool cannonAimed;
        private int trapTrickPick;
        private ClientCard terrorsFieldTarget;
        private readonly List<int> setUpChosenIds = new List<int>();
        // Back Jack stacks the deck; the knowledge is void as soon as a card leaves the deck another way.
        private bool deckTopKnown;
        private bool deckTopIsWanted;

        public LabrynthGenesysExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // The parent registers the TCG list's package; this list keeps only what it plays.
            Executors.Clear();

            // triggers
            AddExecutor(ExecutorType.Repos, CardId.LovelyLabrynthOfTheSilverCastle, ReposForLabrynth);
            AddExecutor(ExecutorType.Activate, CardId.ChaosAngel, ChaosAngelActivate);
            AddExecutor(ExecutorType.Activate, CardId.LovelyLabrynthOfTheSilverCastle, LovelyActivate);
            AddExecutor(ExecutorType.Activate, CardId.AriannaTheLabrynthServant, AriannaTheLabrynthServantActivate);
            AddExecutor(ExecutorType.Activate, CardId.ArianeTheLabrynthServant, ArianeActivate);
            AddExecutor(ExecutorType.Activate, CardId.LabrynthChandraglier, RecycleActivate);
            AddExecutor(ExecutorType.Activate, CardId.LabrynthStovieTorbie, RecycleActivate);
            AddExecutor(ExecutorType.Activate, CardId.LabrynthCooclock, RecycleActivate);
            AddExecutor(ExecutorType.Activate, CardId.WelcomeLabrynth, RecycleActivate);
            AddExecutor(ExecutorType.Activate, CardId.AbsoluteKingBackJack, BackJackActivate);
            AddExecutor(ExecutorType.Activate, CardId.TitanikladTheAshDragon, TitanikladActivate);
            AddExecutor(ExecutorType.Activate, CardId.EnigmasterPackbit, PackbitActivate);
            AddExecutor(ExecutorType.Activate, CardId.VallonTheSuperPsySkyblaster, VallonActivate);
            AddExecutor(ExecutorType.Activate, CardId.DogmatikaFleurdelis, FleurdelisActivate);
            AddExecutor(ExecutorType.Activate, CardId.LabrynthLabyrinth, LabyrinthActivate);

            AddExecutor(ExecutorType.Repos, CardId.ArianeTheLabrynthServant, ReposForLabrynth);
            AddExecutor(ExecutorType.Repos, CardId.AriannaTheLabrynthServant, ReposForLabrynth);

            // chain
            AddExecutor(ExecutorType.Activate, CardId.LadyLabrynthOfTheSilverCastle, LadyLabrynthOfTheSilverCastleFieldActivate);
            AddExecutor(ExecutorType.Activate, CardId.AriasTheLabrynthButler, RecycleActivate);
            AddExecutor(ExecutorType.Activate, CardId.SimultaneousEquationCannons, CannonsActivate);
            AddExecutor(ExecutorType.Activate, CardId.DogmatikaPunishment, PunishmentActivate);
            AddExecutor(ExecutorType.Activate, CardId.DestructiveDarumaKarmaCannon, DestructiveDarumaKarmaCannonActivate);
            AddExecutor(ExecutorType.Activate, CardId.TerrorsOfTheOverroot, TerrorsActivate);
            AddExecutor(ExecutorType.Activate, CardId.TrapTrick, TrapTrickActivate);

            // special summon
            AddExecutor(ExecutorType.Activate, CardId.LadyLabrynthOfTheSilverCastle, LadyLabrynthOfTheSilverCastleHandActivate);
            AddExecutor(ExecutorType.Activate, CardId.BigWelcomeLabrynth, BigWelcomeLabrynthBecomeTargetActivate);
            AddExecutor(ExecutorType.Activate, CardId.WelcomeLabrynth, WelcomeLabrynthActivate);
            AddExecutor(ExecutorType.Activate, CardId.BigWelcomeLabrynth, BigWelcomeLabrynthActivate);

            AddExecutor(ExecutorType.Activate, CardId.AriasTheLabrynthButler, AriasActivate);
            AddExecutor(ExecutorType.Activate, CardId.LabrynthCooclock, LabrynthCooclockActivate);
            AddExecutor(ExecutorType.Activate, CardId.BigWelcomeLabrynth, BigWelcomeGraveActivate);
            AddExecutor(ExecutorType.Activate, CardId.FossilMachineSkullWagon, SkullWagonActivate);
            AddExecutor(ExecutorType.Activate, CardId.LabrynthSetUp, SetUpActivate);

            // normal summon
            AddExecutor(ExecutorType.SpellSet, SpellSetForCooClockCheck);
            AddExecutor(ExecutorType.Summon, CardId.AriannaTheLabrynthServant, AriannaTheLabrynthServantSummon);
            AddExecutor(ExecutorType.Summon, CardId.ArianeTheLabrynthServant, ArianeSummon);
            AddExecutor(ExecutorType.Summon, LabrynthForCooClockSummon);
            AddExecutor(ExecutorType.Summon, ForSynchroSummon);

            AddExecutor(ExecutorType.Activate, CardId.LabrynthChandraglier, FurnitureSetWelcomeActivate);
            AddExecutor(ExecutorType.Activate, CardId.LabrynthStovieTorbie, FurnitureSetWelcomeActivate);

            AddExecutor(ExecutorType.SpSummon, CardId.ChaosAngel, ChaosAngelSpSummonWith2Monster);
            AddExecutor(ExecutorType.SpSummon, CardId.ChaosAngel, ChaosAngelSpSummonWith3Monster);

            AddExecutor(ExecutorType.Repos, MonsterRepos);
            AddExecutor(ExecutorType.Summon, BeatdownSummon);
            AddExecutor(ExecutorType.SummonOrSet, ForBigWelcomeSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.LabrynthStovieTorbie, LastCardWallSet);
            AddExecutor(ExecutorType.MonsterSet, CardId.LabrynthChandraglier, LastCardWallSet);
            AddExecutor(ExecutorType.MonsterSet, CardId.LabrynthCooclock, LastCardWallSet);
            AddExecutor(ExecutorType.SummonOrSet, CardId.ArianeTheLabrynthServant, EmptyFieldSummon);
            AddExecutor(ExecutorType.MonsterSet, CardId.AbsoluteKingBackJack, BackJackSet);
            AddExecutor(ExecutorType.SpellSet, BackrowSet);
        }

        #region card data

        private static bool HasDataType(int id, CardType type)
        {
            NamedCard data = NamedCard.Get(id);
            return data != null && data.HasType(type);
        }

        private static bool IsNormalTrap(int id)
        {
            NamedCard data = NamedCard.Get(id);
            return data != null && data.Type == (int)CardType.Trap;
        }

        private static int DataLevel(int id)
        {
            NamedCard data = NamedCard.Get(id);
            return data == null ? 0 : data.Level;
        }

        private static int DataAttack(int id)
        {
            NamedCard data = NamedCard.Get(id);
            return data == null ? 0 : data.Attack;
        }

        private static bool IsFiend(int id)
        {
            NamedCard data = NamedCard.Get(id);
            return data != null && data.HasType(CardType.Monster) && (data.Race & (int)CardRace.Fiend) != 0;
        }

        private static int LevelOrRank(ClientCard monster)
        {
            return monster.HasType(CardType.Xyz) ? monster.Rank : monster.Level;
        }

        #endregion

        #region board reading

        private int RemovalValue(ClientCard card)
        {
            if (card == null || card.IsFacedown()) return 0;
            if (card.Location == CardLocation.SpellZone)
                return card.IsFloodgate() ? 100 : 0;
            int value = card.Attack / 100;
            if (card.IsFloodgate()) value += 100;
            if (card.IsMonsterDangerous()) value += 60;
            if (card.IsMonsterInvincible()) value += 40;
            if (card.HasType(CardType.Fusion | CardType.Synchro | CardType.Xyz | CardType.Ritual)
                || (card.HasType(CardType.Link) && card.LinkCount >= 2)) value += 30;
            if (Bot.UnderAttack && card == Enemy.BattlingMonster) value += 50;
            if (Duel.Player == 0 && Bot.GetMonsterCount() > 0 && card.GetDefensePower() >= Util.GetBestAttack(Bot)) value += 20;
            return value;
        }

        private bool RemovalTiming()
        {
            if (Duel.LastChainPlayer == 0) return false;
            if (Duel.Player == 0) return Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2;
            return true;
        }

        private List<ClientCard> EnemyCardsByThreat()
        {
            List<ClientCard> result = GetProblematicEnemyCardList(false);
            result.AddRange(Enemy.GetMonsters().Where(card => !result.Contains(card)).OrderByDescending(card => card.IsFaceup() ? card.Attack : 0));
            result.AddRange(Enemy.GetSpells().Where(card => !result.Contains(card) && card.IsFaceup()));
            result.AddRange(Enemy.GetSpells().Where(card => !result.Contains(card)));
            return result.Where(card => !currentDestroyCardList.Contains(card)).ToList();
        }

        private int FreeBackrowCount()
        {
            int count = 0;
            for (int i = 0; i < 5; ++i)
                if (Bot.SpellZone[i] == null) count++;
            return count;
        }

        private bool ControlsFaceupFiend()
        {
            return Bot.GetMonsters().Any(card => card.IsFaceup() && card.HasRace(CardRace.Fiend));
        }

        #endregion

        #region Simultaneous Equation Cannons

        private int CannonPieceValue(int id)
        {
            switch (id)
            {
                case CardId.TitanikladTheAshDragon: return Bot.HasInDeck(CardId.DogmatikaFleurdelis) ? 5 : 3;
                case CardId.VallonTheSuperPsySkyblaster: return 3;
                case CardId.FossilMachineSkullWagon: return 3;
                case CardId.EvilswarmExcitonKnight: return 2;
                default: return 1;
            }
        }

        // The trap banishes 1 Fusion + 2 same-Rank Xyz adding up to every card in both hands and
        // fields, then may return 1 Xyz + 1 Fusion adding up to an opposing monster's Level/Rank to
        // banish the opponent's field. Only plans that reach the second half are returned.
        private CannonPlan FindCannonPlan(int total)
        {
            List<ClientCard> extra = Bot.ExtraDeck.Where(card => card != null && card.Id != 0).ToList();
            List<ClientCard> fusions = extra.Where(card => HasDataType(card.Id, CardType.Fusion)).ToList();
            List<ClientCard> xyzs = extra.Where(card => HasDataType(card.Id, CardType.Xyz)).ToList();
            List<ClientCard> banished = Bot.Banished.Where(card => card != null && card.Id != 0
                && (HasDataType(card.Id, CardType.Fusion) || HasDataType(card.Id, CardType.Xyz))).ToList();
            List<ClientCard> targets = Enemy.GetMonsters().Where(card => card.IsFaceup() && !card.HasType(CardType.Link) && LevelOrRank(card) > 0)
                .OrderByDescending(card => RemovalValue(card)).ToList();
            if (targets.Count == 0) return null;

            CannonPlan best = null;
            foreach (ClientCard fusion in fusions)
            {
                for (int i = 0; i < xyzs.Count - 1; ++i)
                {
                    for (int j = i + 1; j < xyzs.Count; ++j)
                    {
                        int rank = DataLevel(xyzs[i].Id);
                        if (rank != DataLevel(xyzs[j].Id) || DataLevel(fusion.Id) + rank * 2 != total) continue;
                        List<ClientCard> pool = new List<ClientCard>(banished) { fusion, xyzs[i], xyzs[j] };
                        int cost = CannonPieceValue(fusion.Id) + CannonPieceValue(xyzs[i].Id) + CannonPieceValue(xyzs[j].Id);
                        foreach (ClientCard backXyz in pool.Where(card => HasDataType(card.Id, CardType.Xyz)))
                        {
                            foreach (ClientCard backFusion in pool.Where(card => HasDataType(card.Id, CardType.Fusion)))
                            {
                                int sum = DataLevel(backXyz.Id) + DataLevel(backFusion.Id);
                                ClientCard target = targets.FirstOrDefault(card => LevelOrRank(card) == sum);
                                if (target == null) continue;
                                int score = CannonPieceValue(backXyz.Id) + CannonPieceValue(backFusion.Id) - cost;
                                if (best == null || score > best.Score)
                                {
                                    best = new CannonPlan
                                    {
                                        Banish = new List<ClientCard> { fusion, xyzs[i], xyzs[j] },
                                        ReturnXyz = backXyz,
                                        ReturnFusion = backFusion,
                                        Target = target,
                                        Score = score
                                    };
                                }
                            }
                        }
                    }
                }
            }
            return best;
        }

        private int CardsInHandsAndFields()
        {
            return Bot.GetFieldHandCount() + Enemy.GetFieldHandCount();
        }

        private bool CannonsWorthFiring(CannonPlan plan)
        {
            if (plan == null) return false;
            return Enemy.GetFieldCount() >= 2 || RemovalValue(plan.Target) >= WorthRemovingValue;
        }

        // The trap resolves before anything it is chained to, so the count seen now is the count it
        // resolves with unless the opponent responds.
        private bool CannonsActivate()
        {
            if (Card.Location != CardLocation.SpellZone) return false;
            CannonPlan plan = FindCannonPlan(CardsInHandsAndFields());
            if (!CannonsWorthFiring(plan)) return false;
            cannonPlan = plan;
            cannonAimed = false;
            return true;
        }

        private IList<ClientCard> SelectForCannons(IList<ClientCard> cards, int min, int max)
        {
            if (cards.All(card => card.Location == CardLocation.Extra))
            {
                // The opponent may have responded, so the plan is redone with the count the trap resolves with.
                if (!cannonAimed)
                {
                    cannonAimed = true;
                    cannonPlan = FindCannonPlan(CardsInHandsAndFields()) ?? cannonPlan;
                }
                if (cannonPlan != null)
                {
                    List<ClientCard> wanted = cannonPlan.Banish.Where(card => cards.Contains(card)).ToList();
                    if (wanted.Count > 0) return Util.CheckSelectCount(wanted, cards, min, max);
                }
                return null;
            }
            if (cards.All(card => card.Controller == 1 && card.Location == CardLocation.MonsterZone))
            {
                if (cannonPlan != null && cards.Contains(cannonPlan.Target))
                    return Util.CheckSelectCount(new List<ClientCard> { cannonPlan.Target }, cards, min, max);
                return Util.CheckSelectCount(cards.OrderByDescending(card => RemovalValue(card)).ToList(), cards, min, max);
            }
            if (cards.All(card => card.Location == CardLocation.Removed))
            {
                List<ClientCard> wanted = new List<ClientCard>();
                if (cannonPlan != null)
                {
                    if (cards.Contains(cannonPlan.ReturnXyz)) wanted.Add(cannonPlan.ReturnXyz);
                    if (cards.Contains(cannonPlan.ReturnFusion)) wanted.Add(cannonPlan.ReturnFusion);
                }
                if (wanted.Count > 0) return Util.CheckSelectCount(wanted, cards, min, max);
            }
            return null;
        }

        #endregion

        #region Dogmatika Punishment

        private ClientCard PunishmentTarget()
        {
            int reach = Bot.ExtraDeck.Where(card => card != null && card.Id != 0).Select(card => DataAttack(card.Id)).DefaultIfEmpty(-1).Max();
            return Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && card.Attack <= reach && CheckCanBeTargeted(card, true, CardType.Trap) && !currentDestroyCardList.Contains(card))
                .OrderByDescending(card => RemovalValue(card)).FirstOrDefault();
        }

        private bool PunishmentWanted()
        {
            ClientCard target = PunishmentTarget();
            return target != null && RemovalTiming() && RemovalValue(target) >= WorthRemovingValue;
        }

        private bool PunishmentActivate()
        {
            if (Card.Location != CardLocation.SpellZone) return false;
            ClientCard target = PunishmentTarget();
            if (target == null) return false;
            bool fire = RemovalTiming() && RemovalValue(target) >= WorthRemovingValue;
            fire |= DefaultOnBecomeTarget();
            if (!fire) return false;
            AI.SelectCard(target);
            currentDestroyCardList.Add(target);
            activatedCardIdList.Add(Card.Id);
            return true;
        }

        // Sends the monster whose Graveyard effect pays off, then the pieces the Cannons need least.
        private IList<ClientCard> SelectPunishmentCost(IList<ClientCard> cards, int min, int max)
        {
            List<int> order = new List<int>();
            if (Bot.HasInDeck(CardId.DogmatikaFleurdelis)) order.Add(CardId.TitanikladTheAshDragon);
            if (Bot.Hand.Count > 0 && Enemy.GetMonsters().Count(card => card.IsFaceup() && !currentDestroyCardList.Contains(card)) > 0)
                order.Add(CardId.EnigmasterPackbit);
            if (Enemy.GetSpellCount() > 0) order.Add(CardId.FossilMachineSkullWagon);
            if (Enemy.GetSpells().Any(card => card.IsFacedown()) || Enemy.GetMonsters().Any(card => card.IsFacedown()))
                order.Add(CardId.VallonTheSuperPsySkyblaster);
            order.AddRange(new[]
            {
                CardId.Number20GigaBrilliant, CardId.EvilswarmExcitonKnight, CardId.FrekiTheRunickFangs, CardId.SuperQuantalMechBeastGrampulse,
                CardId.VallonTheSuperPsySkyblaster, CardId.FossilMachineSkullWagon, CardId.TitanikladTheAshDragon, CardId.EnigmasterPackbit,
                CardId.ChaosAngel, CardId.GhostrickSocuteboss, CardId.Number65DjinnBuster, CardId.Number49FortuneTune,
                CardId.GeriTheRunickFangs, CardId.HuginTheRunickWings
            });
            foreach (int id in order)
            {
                ClientCard pick = cards.FirstOrDefault(card => card.IsCode(id));
                if (pick != null) return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
            }
            return null;
        }

        #endregion

        #region Terrors of the Overroot

        // Lower is better: the card the opponent gets back should be the one they can use least.
        private int TerrorsGiftValue(ClientCard card)
        {
            NamedCard data = NamedCard.Get(card.Id);
            if (data == null) return 50;
            if (!data.HasType(CardType.Monster)) return 60;
            return Math.Max(data.Attack, data.Defense) / 100 + (data.IsExtraCard() ? 30 : 0);
        }

        private ClientCard TerrorsTarget()
        {
            List<ClientCard> candidates = Enemy.GetMonsters().Where(card => card.IsFaceup()).ToList();
            candidates.AddRange(Enemy.GetSpells().Where(card => card.IsFaceup()));
            return candidates.Where(card => CheckCanBeTargeted(card, true, CardType.Trap) && !currentDestroyCardList.Contains(card))
                .OrderByDescending(card => RemovalValue(card)).FirstOrDefault();
        }

        private bool TerrorsWanted()
        {
            if (!RemovalTiming() || Enemy.Graveyard.Count == 0) return false;
            ClientCard target = TerrorsTarget();
            if (target == null) return false;
            int value = RemovalValue(target);
            int gift = Enemy.Graveyard.Min(card => TerrorsGiftValue(card));
            return value >= WorthRemovingValue && value >= gift + 10;
        }

        private bool TerrorsActivate()
        {
            if (Card.Location != CardLocation.SpellZone) return false;
            bool fire = TerrorsWanted();
            fire |= DefaultOnBecomeTarget() && Enemy.Graveyard.Count > 0 && RemovalValue(TerrorsTarget()) > 0;
            if (!fire) return false;
            terrorsFieldTarget = TerrorsTarget();
            if (terrorsFieldTarget != null) currentDestroyCardList.Add(terrorsFieldTarget);
            activatedCardIdList.Add(Card.Id);
            return true;
        }

        private IList<ClientCard> SelectForTerrors(IList<ClientCard> cards, int min, int max)
        {
            if (cards.All(card => card.Location == CardLocation.Grave))
                return Util.CheckSelectCount(cards.OrderBy(card => TerrorsGiftValue(card)).ToList(), cards, min, max);
            if (terrorsFieldTarget != null && cards.Contains(terrorsFieldTarget))
                return Util.CheckSelectCount(new List<ClientCard> { terrorsFieldTarget }, cards, min, max);
            return Util.CheckSelectCount(cards.OrderByDescending(card => RemovalValue(card)).ToList(), cards, min, max);
        }

        #endregion

        #region Destructive Daruma Karma Cannon

        // Monsters that cannot be turned face-down (Links, Tokens) are sent to the Graveyard;
        // everything else only loses its turn.
        private bool DarumaWanted()
        {
            if (Duel.LastChainPlayer == 0 || PunishmentOnChain()) return false;
            List<ClientCard> faceups = Enemy.GetMonsters().Where(card => card.IsFaceup()).ToList();
            if (faceups.Count == 0) return false;
            List<ClientCard> sent = faceups.Where(card => card.HasType(CardType.Link) || card.HasType(CardType.Token)).ToList();
            int linkRating = sent.Where(card => card.HasType(CardType.Link)).Sum(card => card.LinkCount);
            if (Duel.Player == 1)
            {
                if (linkRating >= 3 || sent.Count >= 2) return true;
                // Flipped monsters are simply flipped back in a Main Phase; in the Battle Phase they lose their attacks.
                bool battle = Duel.Phase >= DuelPhase.BattleStart && Duel.Phase <= DuelPhase.Battle;
                if (battle && faceups.Any(card => card.IsAttack()) && (Bot.GetMonsterCount() == 0 || Util.IsOneEnemyBetter(true))) return true;
                return false;
            }
            if (Duel.Phase != DuelPhase.Main1 || Duel.Turn == 1) return false;
            return linkRating >= 3 || (sent.Count == faceups.Count && Bot.GetMonsters().Any(card => card.Attack >= 1500));
        }

        // Dogmatika Punishment needs its target face-up when it resolves.
        private bool PunishmentOnChain()
        {
            return Duel.CurrentChain.Any(card => card != null && card.Controller == 0 && card.IsCode(CardId.DogmatikaPunishment));
        }

        public override bool DestructiveDarumaKarmaCannonActivateCheck(bool noSelect = false)
        {
            // The parent's rules are written for the opponent's turn; on ours they would only flip our own monsters.
            bool parentRule = Duel.Player == 1 || (Card.Location == CardLocation.SpellZone && DefaultOnBecomeTarget());
            if (PunishmentOnChain()) return false;
            if (parentRule && base.DestructiveDarumaKarmaCannonActivateCheck(noSelect)) return true;
            if (!DarumaWanted()) return false;
            if (!noSelect)
                currentDestroyCardList.AddRange(Enemy.GetMonsters().Where(card => card.HasType(CardType.Link) || card.HasType(CardType.Token)));
            return true;
        }

        #endregion

        #region trap access

        // Traps worth having face-down right now, most urgent first.
        private List<int> TrapWishList()
        {
            List<int> order = new List<int>();
            if (PunishmentTarget() != null && RemovalValue(PunishmentTarget()) >= WorthRemovingValue) order.Add(CardId.DogmatikaPunishment);
            if (Enemy.GetMonsters().Count(card => card.IsFaceup()) >= 2) order.Add(CardId.DestructiveDarumaKarmaCannon);
            if (!Bot.HasInHandOrInSpellZone(CardId.BigWelcomeLabrynth) && !Bot.HasInGraveyard(CardId.BigWelcomeLabrynth)) order.Add(CardId.BigWelcomeLabrynth);
            if (!Bot.HasInHandOrInSpellZone(CardId.WelcomeLabrynth) && !Bot.HasInGraveyard(CardId.WelcomeLabrynth)) order.Add(CardId.WelcomeLabrynth);
            order.AddRange(new[]
            {
                CardId.DestructiveDarumaKarmaCannon, CardId.DogmatikaPunishment, CardId.TerrorsOfTheOverroot, CardId.BigWelcomeLabrynth,
                CardId.TrapTrick, CardId.WelcomeLabrynth, CardId.SimultaneousEquationCannons
            });
            // A copy already face-down adds nothing; a different trap does.
            return order.Distinct().OrderBy(id => Bot.GetSpells().Any(card => card.IsFacedown() && card.IsCode(id)) ? 1 : 0).ToList();
        }

        private IList<ClientCard> SelectByWishList(IList<ClientCard> cards, int min, int max, IList<int> excluded = null)
        {
            foreach (int id in TrapWishList())
            {
                if (excluded != null && excluded.Contains(id)) continue;
                ClientCard pick = cards.FirstOrDefault(card => card.IsCode(id));
                if (pick != null) return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
            }
            return null;
        }

        // Trap Trick needs two copies in the Deck: one is banished, the other is Set and live this turn.
        private int TrapTrickChoice()
        {
            Func<int, bool> available = id => Bot.GetCardCountInDeck(id) >= 2;
            if (available(CardId.SimultaneousEquationCannons) && CannonsWorthFiring(FindCannonPlan(CardsInHandsAndFields())))
                return CardId.SimultaneousEquationCannons;
            if (available(CardId.DogmatikaPunishment) && PunishmentWanted()) return CardId.DogmatikaPunishment;
            if (available(CardId.DestructiveDarumaKarmaCannon) && DarumaWanted()) return CardId.DestructiveDarumaKarmaCannon;
            if (available(CardId.TerrorsOfTheOverroot) && TerrorsWanted()) return CardId.TerrorsOfTheOverroot;

            bool welcomeWindow = Duel.Player == 0 ? (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2) : Duel.Phase == DuelPhase.End;
            welcomeWindow |= Bot.UnderAttack && Bot.GetMonsterCount() == 0;
            if (!welcomeWindow || GetEmptyMainMonsterZoneCount() == 0) return 0;
            bool lovelyReachable = Bot.HasInMonstersZone(CardId.LovelyLabrynthOfTheSilverCastle, true, false, true)
                || CheckBigWelcomeCanSpSummon(CardId.LovelyLabrynthOfTheSilverCastle);
            if (available(CardId.BigWelcomeLabrynth) && !activatedCardIdList.Contains(CardId.BigWelcomeLabrynth)
                && Bot.GetMonsterCount() > 0 && lovelyReachable && Enemy.GetFieldCount() > 0)
                return CardId.BigWelcomeLabrynth;
            if (available(CardId.WelcomeLabrynth) && !activatedCardIdList.Contains(CardId.WelcomeLabrynth)
                && !Bot.GetSpells().Any(card => card.IsFacedown() && card.IsCode(CardId.WelcomeLabrynth, CardId.BigWelcomeLabrynth)))
                return CardId.WelcomeLabrynth;
            return 0;
        }

        private bool TrapTrickActivate()
        {
            if (Card.Location != CardLocation.SpellZone || Duel.LastChainPlayer == 0) return false;
            int choice = TrapTrickChoice();
            if (choice == 0 && !DefaultOnBecomeTarget()) return false;
            trapTrickPick = choice;
            activatedCardIdList.Add(Card.Id);
            return true;
        }

        private IList<ClientCard> SelectForTrapTrick(IList<ClientCard> cards, int min, int max)
        {
            ClientCard pick = cards.FirstOrDefault(card => card.IsCode(trapTrickPick));
            if (pick != null)
            {
                SelectSTPlace(null, true);
                return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
            }
            return SelectByWishList(cards, min, max);
        }

        private int SetUpShuffleValue(ClientCard card)
        {
            if (card.Location == CardLocation.Removed) return 100;
            if (card.IsCode(CardId.LabrynthLabyrinth)) return 80;
            int copies = Bot.Graveyard.Count(other => other.IsCode(card.Id));
            if (card.IsCode(CardId.WelcomeLabrynth)) return copies >= 2 ? 60 : 20;
            return copies >= 2 ? 50 : 10;
        }

        private bool SetUpActivate()
        {
            if (Duel.LastChainPlayer == 0 || !ControlsFaceupFiend()) return false;
            int needed = Card.Location == CardLocation.Hand ? 3 : 2;
            if (FreeBackrowCount() < needed) return false;
            int names = new[]
            {
                CardId.DestructiveDarumaKarmaCannon, CardId.DogmatikaPunishment, CardId.TerrorsOfTheOverroot,
                CardId.TrapTrick, CardId.SimultaneousEquationCannons
            }.Count(id => Bot.HasInDeck(id));
            if (names < 2) return false;
            bool window = Duel.Player == 0 || Duel.Phase == DuelPhase.End || DefaultOnBecomeTarget();
            if (!window) return false;
            setUpChosenIds.Clear();
            activatedCardIdList.Add(Card.Id);
            return true;
        }

        private IList<ClientCard> SelectSetUpTraps(IList<ClientCard> cards, int min, int max)
        {
            IList<ClientCard> picked = SelectByWishList(cards, min, max, setUpChosenIds);
            if (picked != null && picked.Count > 0) setUpChosenIds.Add(picked[0].Id);
            return picked;
        }

        #endregion

        #region Labrynth pieces

        private bool LovelyActivate()
        {
            if (ActivateDescription != Util.GetStringId(CardId.LovelyLabrynthOfTheSilverCastle, 0))
                return LovelyLabrynthOfTheSilverCastleActivate();
            if (CheckWhetherNegated()) return false;
            foreach (int id in TrapWishList())
            {
                if (!Bot.HasInGraveyard(id)) continue;
                AI.SelectCard(id);
                SelectSTPlace(null, true);
                activatedCardIdList.Add(Card.Id);
                return true;
            }
            return false;
        }

        private ClientCard ArianeCost()
        {
            List<ClientCard> hand = Bot.Hand.Where(card => IsNormalTrap(card.Id)).ToList();
            // Welcome Labrynth Sets itself back from the Graveyard, so it is the free cost.
            ClientCard welcome = hand.FirstOrDefault(card => card.IsCode(CardId.WelcomeLabrynth))
                ?? Bot.GetSpells().FirstOrDefault(card => card.IsFacedown() && card.IsCode(CardId.WelcomeLabrynth) && setTrapThisTurn.Contains(card));
            if (welcome != null) return welcome;
            ClientCard duplicate = hand.FirstOrDefault(card => hand.Count(other => other.IsCode(card.Id)) >= 2);
            if (duplicate != null) return duplicate;
            int[] order =
            {
                CardId.SimultaneousEquationCannons, CardId.TrapTrick, CardId.TerrorsOfTheOverroot,
                CardId.DestructiveDarumaKarmaCannon, CardId.DogmatikaPunishment
            };
            foreach (int id in order)
            {
                ClientCard cost = hand.FirstOrDefault(card => card.IsCode(id));
                if (cost != null) return cost;
            }
            return null;
        }

        private int ArianeSummonChoice()
        {
            if (!activatedCardIdList.Contains(CardId.AriannaTheLabrynthServant) && Bot.HasInDeck(CardId.AriannaTheLabrynthServant)
                && DefaultCheckWhetherBotCanSearch())
                return CardId.AriannaTheLabrynthServant;
            int[] furniture = { CardId.LabrynthStovieTorbie, CardId.LabrynthChandraglier, CardId.LabrynthCooclock };
            foreach (int id in furniture)
                if (Bot.HasInDeck(id) && !Bot.HasInHandOrInMonstersZoneOrInGraveyard(id)) return id;
            return 0;
        }

        private bool ArianeSummon()
        {
            if (Duel.Turn > 1 && Enemy.GetMonsterCount() == 0)
            {
                summoned = true;
                return true;
            }
            if (activatedCardIdList.Contains(Card.Id) || CheckWhetherNegated(true, true)) return false;
            if (ArianeCost() == null || ArianeSummonChoice() == 0) return false;
            summoned = true;
            return true;
        }

        private bool ArianeActivate()
        {
            if (ActivateDescription != Util.GetStringId(CardId.ArianeTheLabrynthServant, 0))
            {
                activatedCardIdList.Add(Card.Id + 1);
                return true;
            }
            if (CheckWhetherNegated() || GetEmptyMainMonsterZoneCount() == 0) return false;
            ClientCard cost = ArianeCost();
            int summon = ArianeSummonChoice();
            if (cost == null || summon == 0) return false;
            AI.SelectCard(cost);
            AI.SelectNextCard(summon);
            activatedCardIdList.Add(Card.Id);
            return true;
        }

        // Arias Sets a Normal Trap from the hand that is live at once: the way to fire a trap drawn this turn.
        private bool AriasActivate()
        {
            if (Card.Location == CardLocation.Grave) return false;
            if (Duel.LastChainPlayer != 0)
            {
                int pick = 0;
                // Arias leaves for the Graveyard as cost, one card fewer for the Cannons' count.
                if (Bot.HasInHand(CardId.SimultaneousEquationCannons) && CannonsWorthFiring(FindCannonPlan(CardsInHandsAndFields() - 1)))
                    pick = CardId.SimultaneousEquationCannons;
                else if (Bot.HasInHand(CardId.DogmatikaPunishment) && !activatedCardIdList.Contains(CardId.DogmatikaPunishment) && PunishmentWanted())
                    pick = CardId.DogmatikaPunishment;
                else if (Bot.HasInHand(CardId.TerrorsOfTheOverroot) && !activatedCardIdList.Contains(CardId.TerrorsOfTheOverroot) && TerrorsWanted())
                    pick = CardId.TerrorsOfTheOverroot;
                if (pick != 0 && FreeBackrowCount() > 0)
                {
                    ClientCard trap = Bot.Hand.First(card => card.IsCode(pick));
                    AI.SelectOption(1);
                    AI.SelectCard(trap);
                    SelectSTPlace(trap, true);
                    activatedCardIdList.Add(Card.Id);
                    return true;
                }
            }
            return AriasTheLabrynthButlerActivate();
        }

        public override ClientCard FurnitureGetCost(bool force = false, List<ClientCard> ignoreList = null)
        {
            if (ignoreList == null) ignoreList = new List<ClientCard>();
            Func<int, ClientCard> inHand = id => Bot.Hand.FirstOrDefault(card => card != Card && !ignoreList.Contains(card)
                && !Duel.CurrentChain.Contains(card) && card.IsCode(id));

            // Back Jack stacks the deck when discarded, then digs for a trap from the Graveyard.
            ClientCard cost = inHand(CardId.AbsoluteKingBackJack);
            if (cost != null) return cost;
            cost = base.FurnitureGetCost(force, ignoreList);
            if (cost != null) return cost;

            List<int> order = new List<int> { CardId.SimultaneousEquationCannons, CardId.TrapTrick, CardId.TerrorsOfTheOverroot };
            if (Bot.HasInSpellZone(CardId.LabrynthLabyrinth)) order.Add(CardId.LabrynthLabyrinth);
            if (force)
            {
                order.AddRange(new[]
                {
                    CardId.LabrynthSetUp, CardId.DogmatikaFleurdelis, CardId.DogmatikaPunishment, CardId.LabrynthLabyrinth
                });
            }
            foreach (int id in order)
            {
                cost = inHand(id);
                if (cost != null) return cost;
            }
            return null;
        }

        private bool BigWelcomeGraveActivate()
        {
            if (Duel.LastChainPlayer == 0) return false;
            // Without a Level 8+ Fiend it can only return our own monster: worth it to dodge an effect or to reuse a Normal Summon.
            bool canHitOpponent = Bot.GetMonsters().Any(card => card.IsFaceup() && card.HasRace(CardRace.Fiend) && card.Level >= 8);
            bool dodge = Duel.LastChainPlayer == 1;
            bool reuseSummon = Duel.Player == 0 && !summoned && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2);
            return (canHitOpponent || dodge || reuseSummon) && BigWelcomeLabrynthGraveActivate();
        }

        private bool LabyrinthActivate()
        {
            if (Card.Location == CardLocation.Hand || Card.IsFacedown())
            {
                if (Bot.GetSpells().Any(card => card != Card && card.IsFaceup() && card.IsCode(CardId.LabrynthLabyrinth))) return false;
                return true;
            }
            if (GetEmptyMainMonsterZoneCount() == 0) return false;
            return LabyrinthSummonOrder().Count > 0;
        }

        // The field spell revives a Fiend from the hand or Graveyard whenever a non-Labrynth Normal Trap is activated.
        private List<int> LabyrinthSummonOrder()
        {
            List<int> order = new List<int>();
            if (!Bot.HasInMonstersZone(CardId.LovelyLabrynthOfTheSilverCastle, true, false, true)) order.Add(CardId.LovelyLabrynthOfTheSilverCastle);
            if (!Bot.HasInMonstersZone(CardId.LadyLabrynthOfTheSilverCastle, true, false, true)) order.Add(CardId.LadyLabrynthOfTheSilverCastle);
            if (!activatedCardIdList.Contains(CardId.AriannaTheLabrynthServant)) order.Add(CardId.AriannaTheLabrynthServant);
            order.AddRange(new[]
            {
                CardId.LovelyLabrynthOfTheSilverCastle, CardId.LadyLabrynthOfTheSilverCastle, CardId.AriasTheLabrynthButler,
                CardId.ArianeTheLabrynthServant, CardId.AriannaTheLabrynthServant, CardId.LabrynthChandraglier, CardId.LabrynthStovieTorbie
            });
            return order.Distinct().Where(id => Bot.Hand.Any(card => card.IsCode(id)) || Bot.HasInGraveyard(id)).ToList();
        }

        #endregion

        #region support monsters

        private bool BackJackActivate()
        {
            if (Card.Location != CardLocation.Grave) return false;
            // On the opponent's turn the offered effect may be the dig; skip it only when the known top card is one to keep.
            if (Duel.Player == 1 && deckTopKnown && !deckTopIsWanted) return false;
            return true;
        }

        private int DeckTopValue(ClientCard card)
        {
            if (IsNormalTrap(card.Id))
            {
                int index = TrapWishList().IndexOf(card.Id);
                return 100 - (index < 0 ? 20 : index);
            }
            // Cards that still work from the Graveyard are the acceptable misses.
            if (card.IsCode(CardId.LabrynthStovieTorbie, CardId.LabrynthChandraglier, CardId.LabrynthCooclock, CardId.AbsoluteKingBackJack)) return 50;
            return 0;
        }

        public override IList<ClientCard> OnCardSorting(IList<ClientCard> cards)
        {
            if (cards.Count > 0 && cards.All(card => card.Controller == 0 && card.Location == CardLocation.Deck))
            {
                List<ClientCard> ordered = cards.OrderByDescending(card => DeckTopValue(card)).ToList();
                deckTopKnown = true;
                deckTopIsWanted = DeckTopValue(ordered[0]) > 0;
                return ordered;
            }
            return base.OnCardSorting(cards);
        }

        private bool FleurdelisActivate()
        {
            if (Card.Location == CardLocation.Hand)
                return GetEmptyMainMonsterZoneCount() > 0 && Duel.LastChainPlayer != 0;
            SelectSTPlace(null, true);
            return true;
        }

        private bool TitanikladActivate()
        {
            return Card.Location == CardLocation.Grave;
        }

        private bool PackbitActivate()
        {
            if (Card.Location != CardLocation.Grave || Bot.Hand.Count == 0) return false;
            ClientCard target = Enemy.GetMonsters()
                .Where(card => card.IsFaceup() && CheckCanBeTargeted(card, true, CardType.Monster) && !currentDestroyCardList.Contains(card))
                .OrderByDescending(card => RemovalValue(card)).FirstOrDefault();
            if (target == null || RemovalValue(target) < 15) return false;
            AI.SelectCard(target);
            currentDestroyCardList.Add(target);
            return true;
        }

        private bool VallonActivate()
        {
            if (Card.Location != CardLocation.Grave) return false;
            List<ClientCard> targets = Enemy.GetSpells().Where(card => card.IsFacedown() && !currentDestroyCardList.Contains(card)).ToList();
            targets.AddRange(Enemy.GetMonsters().Where(card => card.IsFacedown() && !currentDestroyCardList.Contains(card)));
            if (targets.Count == 0) return false;
            AI.SelectCard(targets);
            currentDestroyCardList.Add(targets[0]);
            return true;
        }

        private bool SkullWagonActivate()
        {
            if (Card.Location != CardLocation.Grave) return false;
            List<ClientCard> targets = Enemy.GetSpells().Where(card => card.IsFaceup() && card.IsFloodgate()).ToList();
            targets.AddRange(Enemy.GetSpells().Where(card => card.IsFaceup() && !targets.Contains(card)
                && card.HasType(CardType.Continuous | CardType.Field | CardType.Equip | CardType.Pendulum) && !notToDestroySpellTrap.Contains(card.Id)));
            targets.AddRange(Enemy.GetSpells().Where(card => card.IsFacedown()));
            if (targets.Count == 0) return false;
            AI.SelectCard(targets);
            return true;
        }

        private int DiscardValue(ClientCard card)
        {
            if (card.IsCode(CardId.AbsoluteKingBackJack)) return 100;
            if (card.IsCode(CardId.LabrynthCooclock)) return 80;
            if (Bot.Hand.Count(other => other.IsCode(card.Id)) >= 2) return 70;
            if (card.IsCode(CardId.SimultaneousEquationCannons, CardId.TrapTrick)) return 60;
            if (card.IsCode(CardId.LabrynthStovieTorbie, CardId.LabrynthChandraglier, CardId.WelcomeLabrynth)) return 50;
            if (card.IsCode(CardId.LovelyLabrynthOfTheSilverCastle, CardId.LadyLabrynthOfTheSilverCastle, CardId.BigWelcomeLabrynth)) return 0;
            return 20;
        }

        #endregion

        #region turn actions

        private bool BeatdownSummon()
        {
            if (Duel.Turn == 1 || Duel.Phase != DuelPhase.Main1 || Enemy.GetMonsterCount() > 0) return false;
            if (Card.Level > 4 || DataAttack(Card.Id) <= 0) return false;
            if (Bot.Hand.Any(card => card != Card && card.IsMonster() && card.Level <= 4 && DataAttack(card.Id) > DataAttack(Card.Id))) return false;
            summoned = true;
            return true;
        }

        // Back Jack only works from the Graveyard; with no furniture to discard it, it goes there as a blocker.
        private bool BackJackSet()
        {
            if (Bot.HasInHand(CardId.LabrynthStovieTorbie) || Bot.HasInHand(CardId.LabrynthChandraglier)) return false;
            if (Duel.Phase != DuelPhase.Main2 && Duel.Turn != 1 && Bot.GetMonsterCount() > 0) return false;
            summoned = true;
            return true;
        }

        // The furniture's hand effects need a second card to discard; alone in hand they are only blockers.
        private bool LastCardWallSet()
        {
            if (Bot.GetMonsterCount() > 0 || Enemy.GetMonsterCount() == 0 || Bot.Hand.Count != 1) return false;
            summoned = true;
            return true;
        }

        private bool EmptyFieldSummon()
        {
            if (Bot.GetMonsterCount() > 0 || Duel.Turn == 1) return false;
            summoned = true;
            return true;
        }

        private bool BackrowSet()
        {
            if (Card.IsCode(CardId.LabrynthLabyrinth)) return false;
            // One zone stays open for the Sets coming from the furniture, Lady, Lovely and Back Jack.
            if (FreeBackrowCount() <= 1) return false;
            if (Card.IsCode(CardId.LabrynthSetUp) && Duel.Player == 0 && ControlsFaceupFiend()) return false;
            if (Card.IsCode(CardId.TrapTrick) && Bot.GetSpells().Any(card => card.IsFacedown() && card.IsCode(CardId.TrapTrick))) return false;
            return SpellSetCheck();
        }

        #endregion

        #region callbacks

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            ClientCard activating = Duel.GetCurrentChainCard();
            IList<ClientCard> picked = null;
            if (solving != null && solving.ActivatePlayer == 0)
                picked = SelectWhileResolving(solving, cards, min, max, hint);
            else if (solving == null && activating != null && activating.Controller == 0 && Duel.LastChainPlayer == 0)
                picked = SelectWhileActivating(activating, cards, min, max, hint);
            if (picked != null) return picked;
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        private IList<ClientCard> SelectWhileActivating(ClientCard activating, IList<ClientCard> cards, int min, int max, int hint)
        {
            if (activating.IsCode(CardId.TerrorsOfTheOverroot))
                return SelectForTerrors(cards, min, max);
            if (activating.IsCode(CardId.LabrynthSetUp) && hint == HintMsg.ToDeck)
                return Util.CheckSelectCount(cards.OrderByDescending(card => SetUpShuffleValue(card)).ToList(), cards, min, max);
            return null;
        }

        private IList<ClientCard> SelectWhileResolving(ChainInfo solving, IList<ClientCard> cards, int min, int max, int hint)
        {
            if (solving.IsActivateCode(CardId.SimultaneousEquationCannons))
                return SelectForCannons(cards, min, max);
            if (solving.IsActivateCode(CardId.TrapTrick))
                return SelectForTrapTrick(cards, min, max);
            if (solving.IsActivateCode(CardId.DogmatikaPunishment) && cards.All(card => card.Location == CardLocation.Extra))
                return SelectPunishmentCost(cards, min, max);
            if (solving.IsActivateCode(CardId.LabrynthSetUp) && hint == HintMsg.Set)
                return SelectSetUpTraps(cards, min, max);
            if (solving.IsActivateCode(CardId.LadyLabrynthOfTheSilverCastle) && hint == HintMsg.Set)
            {
                IList<ClientCard> trap = SelectByWishList(cards, min, max);
                if (trap != null) SelectSTPlace(null, true);
                return trap;
            }
            if (solving.IsActivateCode(CardId.LabrynthLabyrinth) && hint == HintMsg.SpSummon)
            {
                foreach (int id in LabyrinthSummonOrder())
                {
                    ClientCard pick = cards.FirstOrDefault(card => card.IsCode(id) && card.Location == CardLocation.Grave)
                        ?? cards.FirstOrDefault(card => card.IsCode(id));
                    if (pick != null) return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
                }
                return null;
            }
            if (solving.IsActivateCode(CardId.WelcomeLabrynth, CardId.BigWelcomeLabrynth) && hint == HintMsg.Destroy)
            {
                List<ClientCard> order = EnemyCardsByThreat().Where(card => cards.Contains(card)).ToList();
                if (order.Count > 0) currentDestroyCardList.Add(order[0]);
                order.AddRange(cards.Where(card => card.Controller == 0 && card.Location == CardLocation.SpellZone));
                return Util.CheckSelectCount(order, cards, min, max);
            }
            if (solving.IsActivateCode(CardId.EnigmasterPackbit) && hint == HintMsg.Discard)
                return Util.CheckSelectCount(cards.OrderByDescending(card => DiscardValue(card)).ToList(), cards, min, max);
            return null;
        }

        public override bool OnSelectYesNo(int desc)
        {
            if (desc == Util.GetStringId(CardId.SimultaneousEquationCannons, 1))
                return true;
            if (desc == Util.GetStringId(CardId.LabrynthSetUp, 0))
                return true;
            if (desc == Util.GetStringId(CardId.LabrynthLabyrinth, 0))
                return EnemyCardsByThreat().Count > 0;
            return base.OnSelectYesNo(desc);
        }

        public override int OnSelectOption(IList<int> options)
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null && solving.ActivatePlayer == 0 && solving.IsActivateCode(CardId.TitanikladTheAshDragon)
                && options.Contains(OptionAddToHand) && options.Contains(OptionSpecialSummon))
            {
                // Fleurdelis only Sets Dogmatika Punishment when Special Summoned from the hand.
                bool keepInHand = Bot.HasInDeck(CardId.DogmatikaPunishment) && Bot.GetMonsterCount() > 0;
                return options.IndexOf(keepInHand ? OptionAddToHand : OptionSpecialSummon);
            }

            int result = base.OnSelectOption(options);
            int[] servants = { CardId.AriannaTheLabrynthServant, CardId.ArianeTheLabrynthServant };
            foreach (int servant in servants)
            {
                int setOption = options.IndexOf(Util.GetStringId(servant, 3));
                int skipOption = options.IndexOf(Util.GetStringId(servant, 4));
                if (setOption < 0 || result != skipOption || FreeBackrowCount() <= 1) continue;
                ClientCard trap = Bot.Hand.FirstOrDefault(card => IsNormalTrap(card.Id)
                    && !Bot.GetSpells().Any(other => other.IsFacedown() && other.IsCode(card.Id)));
                if (trap == null) continue;
                AI.SelectCard(trap);
                return setOption;
            }
            return result;
        }

        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            // Yubel and its forms cannot be destroyed by battle and turn the attack into damage to the attacker's side.
            if (defender.IsFaceup() && !defender.IsDisabled() && defender.IsCode(YubelForms)) return false;
            return base.OnPreBattleBetween(attacker, defender);
        }

        public override bool OnPreActivate(ClientCard card)
        {
            // Anything chained on top of the Cannons resolves first and changes the count they were aimed at.
            if (Duel.CurrentChain.Any(chained => chained != null && chained.Controller == 0 && chained.IsCode(CardId.SimultaneousEquationCannons)))
                return false;
            return base.OnPreActivate(card);
        }

        public override void OnNewTurn()
        {
            cannonPlan = null;
            cannonAimed = false;
            trapTrickPick = 0;
            terrorsFieldTarget = null;
            setUpChosenIds.Clear();
            base.OnNewTurn();
        }

        public override void OnDraw(int player)
        {
            if (player == 0) deckTopKnown = false;
            base.OnDraw(player);
        }

        public override void OnChainEnd()
        {
            terrorsFieldTarget = null;
            base.OnChainEnd();
        }

        public override void OnMove(ClientCard card, int previousControler, int previousLocation, int currentControler, int currentLocation)
        {
            base.OnMove(card, previousControler, previousLocation, currentControler, currentLocation);

            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            bool ownEffect = solving != null && solving.ActivatePlayer == 0;
            if (previousControler == 0 && previousLocation == (int)CardLocation.Deck)
                deckTopKnown = false;

            // These effects Set a trap that is live the same turn, so it is not a "Set this turn" card for the Cooclock planning.
            if (card != null && currentControler == 0 && currentLocation == (int)CardLocation.SpellZone && ownEffect
                && solving.IsActivateCode(CardId.TrapTrick, CardId.AbsoluteKingBackJack, CardId.DogmatikaFleurdelis))
                setTrapThisTurn.Remove(card);
        }

        #endregion
    }
}
