using System;
using System.Collections.Generic;
using System.Linq;

namespace RouletteGame
{
    // Enum for bet types
    public enum BetType
    {
        Dozen,
        OddEven,
        RedBlack,
        Straight
    }

    // Enum for feature states
    public enum FeatureState
    {
        Normal,
        Weakened,
        Exited
    }

    // Enum for payout strategy
    public enum PayoutStrategy
    {
        Random,  // Randomly select from valid numbers
        Low,     // Select numbers with payout in lowest 25%
        Medium,  // Select numbers with payout in 25%-75%
        High     // Select numbers with payout in highest 25%
    }

    // Configuration class for all game settings
    public static class GameConfiguration
    {
        // Roulette Numbers Configuration
        public static class Numbers
        {
            public static readonly int[] All = Enumerable.Range(0, 37).ToArray(); // 0 to 36
            public static readonly int[] Red = new[] { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
            public static readonly int[] Black = new[] { 2, 4, 6, 8, 10, 11, 13, 15, 17, 20, 22, 24, 26, 28, 29, 31, 33, 35 };
            public static readonly int[] Even = All.Where(n => n != 0 && n % 2 == 0).ToArray();
            public static readonly int[] Odd = All.Where(n => n % 2 == 1).ToArray();
            public static readonly int[] Dozen1 = All.Where(n => n >= 1 && n <= 12).ToArray();
            public static readonly int[] Dozen2 = All.Where(n => n >= 13 && n <= 24).ToArray();
            public static readonly int[] Dozen3 = All.Where(n => n >= 25 && n <= 36).ToArray();
        }

        // Bet Odds Configuration
        public static readonly Dictionary<BetType, int> BetOdds = new Dictionary<BetType, int>
        {
            { BetType.Dozen, 2 },
            { BetType.OddEven, 1 },
            { BetType.RedBlack, 1 },
            { BetType.Straight, 35 }
        };

        // Pity System Configuration
        public static class PitySystem
        {
            // Feature State Probabilities
            public static class Probabilities
            {
                public const double Normal = 0.80;    // 80% win chance in Normal state
                public const double Weakened = 0.60;  // 60% win chance in Weakened state
                public const double Exited = 0.0;     // No win boost in Exited state
            }

            // Threshold Multipliers
            public static class Thresholds
            {
                public const double NormalState = 2.0;    // Net loss >= threshold * 2.0 for Normal state
                public const double WeakenedState = 1.5;  // Net loss >= threshold * 1.5 for Weakened state
            }

            // Dynamic Threshold Configuration
            public static class Dynamic
            {
                public const double VariancePercentage = 0.05;  // 5% variance for dynamic thresholds
                public const double MaxOddsMultiplier = 36.0;   // Maximum odds in roulette
                public const double DefaultPayoutMultiplier = 1.2; // Default payout multiplier for max win amount
                public const double DefaultMaxWinMultiplier = 0.5; // Default max win amount multiplier
            }

            // Feature Cooldown
            public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(30);
        }

        // Default Values
        public static class Defaults
        {
            public const double Balance = 1000.0;
            public const double NetLoss = 1000.0;
            public const int DaysWindow = 7;
            public const double LossThreshold = 1000.0;
            public const double MaxBet = 1000.0;
            public const double BaseRTP = 0.973;  // Base RTP (~2.7% house edge)
        }
    }

    // Class to represent a bet
    public class Bet
    {
        public BetType BetType { get; set; }
        public double Amount { get; set; }
        public string Selection { get; set; } // e.g., "red", "odd", "dozen_1", or number like "5"

        public Bet(BetType betType, double amount, string selection)
        {
            BetType = betType;
            Amount = amount;
            Selection = selection;
        }
    }

    // Class to represent a player
    public class Player
    {
        public double Balance { get; set; }
        public List<Bet> Bets { get; set; }
        public double NetLoss { get; set; }
        public int DaysWindow { get; set; }
        public double LossThreshold { get; set; }

        public Player(double balance, double netLoss, int daysWindow, double lossThreshold)
        {
            Balance = balance;
            Bets = new List<Bet>();
            NetLoss = netLoss;
            DaysWindow = daysWindow;
            LossThreshold = lossThreshold;
        }
    }

    public class RouletteGame
    {
        private readonly Player _player;
        private readonly double _thresholdLoss;
        private readonly int _daysWindow;
        private readonly PayoutStrategy _payoutStrategy;
        private double _maxPayoutThreshold;
        private FeatureState _featureState;
        private readonly double _rtpBase;
        private readonly Random _random;
        private DateTime _lastFeatureActivation;
        private readonly TimeSpan _featureCooldown;

        // New properties for dynamic thresholds
        private double _maxBet;
        private double _currentThresholdLoss;
        private double _currentMaxWinAmount;

        public RouletteGame(Player player, double maxBet, double thresholdLoss = 1000, int daysWindow = 7, PayoutStrategy payoutStrategy = PayoutStrategy.Random)
        {
            _player = player;
            _maxBet = maxBet;
            _thresholdLoss = thresholdLoss;
            _daysWindow = daysWindow;
            _payoutStrategy = payoutStrategy;
            _featureState = FeatureState.Exited;
            _random = new Random();
            _rtpBase = GameConfiguration.Defaults.BaseRTP;
            _featureCooldown = GameConfiguration.PitySystem.Cooldown;
            UpdateDynamicThresholds();
        }

        private void UpdateDynamicThresholds()
        {
            // Calculate base threshold based on max bet
            double baseThreshold = _maxBet * GameConfiguration.PitySystem.Dynamic.MaxOddsMultiplier;

            // Add random variance to threshold (±5%)
            double variance = baseThreshold * GameConfiguration.PitySystem.Dynamic.VariancePercentage;
            _currentThresholdLoss = baseThreshold + (_random.NextDouble() * 2 - 1) * variance;

            // Calculate max win amount based on threshold (±5%)
            variance = _currentThresholdLoss * GameConfiguration.PitySystem.Dynamic.VariancePercentage;
            _currentMaxWinAmount = _currentThresholdLoss + (_random.NextDouble() * 2 - 1) * variance;
        }

        private void UpdateFeatureState()
        {
            // Determine the current state of the Guaranteed Win Feature based on net loss
            if (_player.NetLoss >= _currentThresholdLoss * GameConfiguration.PitySystem.Thresholds.NormalState)
                _featureState = FeatureState.Normal;
            else if (_player.NetLoss >= _currentThresholdLoss * GameConfiguration.PitySystem.Thresholds.WeakenedState)
                _featureState = FeatureState.Weakened;
            else
                _featureState = FeatureState.Exited;
        }

        private double GetWinProbability()
        {
            // Return win probability based on feature state
            return _featureState switch
            {
                FeatureState.Normal => GameConfiguration.PitySystem.Probabilities.Normal,
                FeatureState.Weakened => GameConfiguration.PitySystem.Probabilities.Weakened,
                _ => GameConfiguration.PitySystem.Probabilities.Exited
            };
        }

        private void UpdateMaxPayoutThreshold(List<Bet> bets)
        {
            // Use current max win amount as base for payout threshold
            double totalBetAmount = bets.Sum(b => b.Amount);
            _maxPayoutThreshold = totalBetAmount > 0
                ? Math.Min(_currentMaxWinAmount, totalBetAmount * GameConfiguration.PitySystem.Dynamic.DefaultPayoutMultiplier)
                : _currentMaxWinAmount * GameConfiguration.PitySystem.Dynamic.DefaultMaxWinMultiplier;
        }

        public void UpdateMaxBet(double newMaxBet)
        {
            _maxBet = newMaxBet;
            UpdateDynamicThresholds();
        }

        public void PlaceBet(Bet bet)
        {
            // Check if bet amount is valid
            if (bet.Amount <= 0)
            {
                throw new ArgumentException("Bet amount must be greater than 0");
            }

            // Add a bet to the player's current bets
            _player.Bets.Add(bet);
        }

        private int DetermineForcedOutcome(List<Bet> bets)
        {
            if (!bets.Any())
                return GameConfiguration.Numbers.All[_random.Next(GameConfiguration.Numbers.All.Length)];

            // Step 1: Collect winning numbers for each bet
            var winningNumbers = new List<(Bet Bet, int[] Numbers)>();
            foreach (var bet in bets)
            {
                int[] numbers = bet.BetType switch
                {
                    BetType.Straight => new[] { int.Parse(bet.Selection) },
                    BetType.RedBlack => bet.Selection == "red" ? GameConfiguration.Numbers.Red : GameConfiguration.Numbers.Black,
                    BetType.OddEven => bet.Selection == "odd" ? GameConfiguration.Numbers.Odd : GameConfiguration.Numbers.Even,
                    BetType.Dozen => bet.Selection switch
                    {
                        "dozen_1" => GameConfiguration.Numbers.Dozen1,
                        "dozen_2" => GameConfiguration.Numbers.Dozen2,
                        _ => GameConfiguration.Numbers.Dozen3
                    },
                    _ => Array.Empty<int>()
                };
                winningNumbers.Add((bet, numbers));
            }

            // Step 2: Calculate potential payout for each number
            var numberPayouts = GameConfiguration.Numbers.All.ToDictionary(n => n, n => 0.0);
            foreach (var n in GameConfiguration.Numbers.All)
            {
                foreach (var (bet, numbers) in winningNumbers)
                {
                    if (numbers.Contains(n))
                    {
                        numberPayouts[n] += bet.Amount * (GameConfiguration.BetOdds[bet.BetType] + 1);
                    }
                }
            }

            // Step 3: Apply payout strategy
            var validNumbers = numberPayouts
                .Where(kvp => kvp.Value > 0)
                .OrderBy(kvp => kvp.Value)
                .Select(kvp => (Number: kvp.Key, Payout: kvp.Value))
                .ToList();

            if (!validNumbers.Any())
                return GameConfiguration.Numbers.All[_random.Next(GameConfiguration.Numbers.All.Length)];

            var count = validNumbers.Count;
            List<(int Number, double Payout)> selectedNumbers;

            switch (_payoutStrategy)
            {
                case PayoutStrategy.Low:
                    // Select from lowest 25% of payouts
                    selectedNumbers = validNumbers.Take(Math.Max(1, count / 4)).ToList();
                    break;
                case PayoutStrategy.Medium:
                    // Select from middle 50% of payouts
                    selectedNumbers = validNumbers.Skip(count / 4).Take(count / 2).ToList();
                    break;
                case PayoutStrategy.High:
                    // Select from highest 25% of payouts
                    selectedNumbers = validNumbers.Skip(3 * count / 4).ToList();
                    break;
                case PayoutStrategy.Random:
                default:
                    // Randomly select from all valid numbers
                    selectedNumbers = validNumbers.ToList();
                    break;
            }

            // If no numbers were selected, fall back to all valid numbers
            if (!selectedNumbers.Any())
            {
                selectedNumbers = validNumbers.ToList();
            }

            // Step 4: Weight the selection based on strategy
            double totalWeight = 0;
            var weightedNumbers = new List<(int Number, double Weight)>();

            foreach (var (number, payout) in selectedNumbers)
            {
                double weight;
                switch (_payoutStrategy)
                {
                    case PayoutStrategy.Low:
                        weight = 1.0 / (payout + 1); // Higher weight for lower payouts
                        break;
                    case PayoutStrategy.High:
                        weight = payout + 1; // Higher weight for higher payouts
                        break;
                    case PayoutStrategy.Medium:
                        weight = 1.0; // Equal weight
                        break;
                    default:
                        weight = 1.0; // Equal weight for random
                        break;
                }
                totalWeight += weight;
                weightedNumbers.Add((number, weight));
            }

            // If no weighted numbers, return a random number
            if (!weightedNumbers.Any())
            {
                return GameConfiguration.Numbers.All[_random.Next(GameConfiguration.Numbers.All.Length)];
            }

            // Step 5: Select a number based on weights
            double randomWeight = _random.NextDouble() * totalWeight;
            double currentWeight = 0;

            foreach (var (number, weight) in weightedNumbers)
            {
                currentWeight += weight;
                if (randomWeight <= currentWeight)
                    return number;
            }

            // Fallback to last number if something goes wrong
            return weightedNumbers.Last().Number;
        }

        private bool CanActivateFeature()
        {
            return DateTime.Now - _lastFeatureActivation >= _featureCooldown;
        }

        public int Spin()
        {
            UpdateFeatureState();
            double winProbability = GetWinProbability();

            // Check cooldown before forcing a win
            bool forceWin = _featureState != FeatureState.Exited && 
                           CanActivateFeature() && 
                           _random.NextDouble() < winProbability;

            if (forceWin && _player.Bets.Any())
            {
                _lastFeatureActivation = DateTime.Now;
                return DetermineForcedOutcome(_player.Bets);
            }

            return GameConfiguration.Numbers.All[_random.Next(GameConfiguration.Numbers.All.Length)];
        }

        public double ResolveBets(int outcome)
        {
            double totalPayout = 0.0;
            double totalBetAmount = _player.Bets.Sum(b => b.Amount);

            foreach (var bet in _player.Bets)
            {
                bool win = false;
                switch (bet.BetType)
                {
                    case BetType.Straight:
                        win = int.Parse(bet.Selection) == outcome;
                        break;
                    case BetType.RedBlack:
                        win = (bet.Selection == "red" && GameConfiguration.Numbers.Red.Contains(outcome)) ||
                              (bet.Selection == "black" && GameConfiguration.Numbers.Black.Contains(outcome));
                        break;
                    case BetType.OddEven:
                        win = (bet.Selection == "odd" && GameConfiguration.Numbers.Odd.Contains(outcome)) ||
                              (bet.Selection == "even" && GameConfiguration.Numbers.Even.Contains(outcome));
                        break;
                    case BetType.Dozen:
                        win = (bet.Selection == "dozen_1" && GameConfiguration.Numbers.Dozen1.Contains(outcome)) ||
                              (bet.Selection == "dozen_2" && GameConfiguration.Numbers.Dozen2.Contains(outcome)) ||
                              (bet.Selection == "dozen_3" && GameConfiguration.Numbers.Dozen3.Contains(outcome));
                        break;
                }

                if (win)
                {
                    double payout = bet.Amount * (GameConfiguration.BetOdds[bet.BetType] + 1);
                    totalPayout += payout;
                }
            }

            // Update player's net loss
            if (totalPayout < totalBetAmount)
            {
                // Player lost or won less than they bet
                _player.NetLoss += (totalBetAmount - totalPayout);
            }

            _player.Bets.Clear();
            return totalPayout;
        }

        public (int Outcome, double Payout) PlayRound(List<Bet> bets)
        {
            // Play a single round of roulette with multiple bets
            foreach (var bet in bets)
            {
                PlaceBet(bet);
            }
            int outcome = Spin();
            double payout = ResolveBets(outcome);
            return (outcome, payout);
        }
    }

    class RtpSimulator
    {
        private readonly Random _random = new Random();

        public void RunSimulation(int sessions = 100000)
        {
            // Simulate 1 million sessions to measure RTP for different PayoutStrategies
            var strategies = new[] { PayoutStrategy.Random, PayoutStrategy.Low, PayoutStrategy.Medium, PayoutStrategy.High };
            var results = new Dictionary<PayoutStrategy, (double TotalBet, double TotalPayout, int ForceWinCount, int NormalWinCount, int InsufficientBalanceCount)>();

            Console.WriteLine("\n=== Starting RTP Simulation ===");
            Console.WriteLine($"Total Sessions: {sessions:N0}");
            Console.WriteLine("=============================");

            foreach (var strategy in strategies)
            {
                results[strategy] = (0.0, 0.0, 0, 0, 0);
                var player = new Player(
                    balance: GameConfiguration.Defaults.Balance,
                    netLoss: GameConfiguration.Defaults.NetLoss,
                    daysWindow: GameConfiguration.Defaults.DaysWindow,
                    lossThreshold: GameConfiguration.Defaults.LossThreshold
                );
                var game = new RouletteGame(
                    player,
                    maxBet: GameConfiguration.Defaults.MaxBet,
                    thresholdLoss: GameConfiguration.Defaults.LossThreshold,
                    daysWindow: GameConfiguration.Defaults.DaysWindow,
                    payoutStrategy: strategy
                );

                for (int i = 0; i < sessions; i++)
                {
                    var bets = GenerateRandomBets();
                    double betAmount = bets.Sum(b => b.Amount);

                    // Check if player has enough balance
                    if (player.Balance < betAmount)
                    {
                        results[strategy] = (
                            results[strategy].TotalBet,
                            results[strategy].TotalPayout,
                            results[strategy].ForceWinCount,
                            results[strategy].NormalWinCount,
                            results[strategy].InsufficientBalanceCount + 1
                        );
                        continue;
                    }

                    // Deduct bet amount from balance
                    player.Balance -= betAmount;

                    var (outcome, payout) = game.PlayRound(bets);
                    
                    // Add payout to balance
                    player.Balance += payout;

                    // Update netLoss based on actual result
                    if (payout == 0) // Lost
                    {
                        player.NetLoss += betAmount;
                    }
                    else // Won
                    {
                        player.NetLoss = Math.Max(0, player.NetLoss - betAmount); // Reduce netLoss by bet amount, but not below 0
                    }

                    var featureState = game.GetType().GetField("_featureState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(game);
                    if (featureState?.ToString() == "Normal" || featureState?.ToString() == "Weakened")
                    {
                        results[strategy] = (
                            results[strategy].TotalBet + betAmount,
                            results[strategy].TotalPayout + payout,
                            results[strategy].ForceWinCount + 1,
                            results[strategy].NormalWinCount,
                            results[strategy].InsufficientBalanceCount
                        );
                    }
                    else
                    {
                        results[strategy] = (
                            results[strategy].TotalBet + betAmount,
                            results[strategy].TotalPayout + payout,
                            results[strategy].ForceWinCount,
                            results[strategy].NormalWinCount + 1,
                            results[strategy].InsufficientBalanceCount
                        );
                    }

                    // Reset balance if it gets too low
                    if (player.Balance < 100)
                    {
                        player.Balance = GameConfiguration.Defaults.Balance;
                    }
                }
            }

            // Print final results for all strategies
            Console.WriteLine("\n=== Final Results ===");
            foreach (var strategy in strategies)
            {
                var (finalTotalBet, finalTotalPayout, forceWinCount, normalWinCount, insufficientBalanceCount) = results[strategy];
                double rtp = finalTotalPayout / finalTotalBet;
                double forceWinRate = (double)forceWinCount / (forceWinCount + normalWinCount);

                Console.WriteLine($"PS: {strategy}, RTP: {rtp:P2}, Total Bet: ${finalTotalBet:N0}, Total Payout: ${finalTotalPayout:N0}, Force Win Rate: {forceWinRate:P2}, Force Win: {forceWinCount:N0}, Normal Win: {normalWinCount:N0}, Insufficient Balance: {insufficientBalanceCount:N0}");
            }
            Console.WriteLine("=====================");
        }

        private List<Bet> GenerateRandomBets()
        {
            var bets = new List<Bet>();
            int numBets = _random.Next(1, 4); // 1-3 bets per round

            // Adjusted probabilities for bet types to increase house edge
            var betTypeProbabilities = new Dictionary<BetType, double>
            {
                { BetType.RedBlack, 0.45 },  // Increased safe bets
                { BetType.OddEven, 0.45 },   // Increased safe bets
                { BetType.Dozen, 0.08 },     // Reduced medium risk bets
                { BetType.Straight, 0.02 }   // Reduced high risk bets
            };

            for (int i = 0; i < numBets; i++)
            {
                // Select bet type using cumulative probability
                double r = _random.NextDouble();
                double cumulative = 0;
                BetType selectedType = BetType.RedBlack; // Default

                foreach (var kvp in betTypeProbabilities)
                {
                    cumulative += kvp.Value;
                    if (r <= cumulative)
                    {
                        selectedType = kvp.Key;
                        break;
                    }
                }

                // Generate bet amount based on bet type
                double amount = selectedType switch
                {
                    BetType.Straight => _random.Next(1, 6) * 5.0,    // $5-$25
                    BetType.Dozen => _random.Next(1, 11) * 5.0,      // $5-$50
                    _ => _random.Next(1, 21) * 5.0                   // $5-$100
                };

                // Select specific bet based on type
                string selection = selectedType switch
                {
                    BetType.RedBlack => _random.NextDouble() < 0.5 ? "red" : "black",
                    BetType.OddEven => _random.NextDouble() < 0.5 ? "odd" : "even",
                    BetType.Dozen => $"dozen_{_random.Next(1, 4)}",
                    _ => _random.Next(0, 37).ToString()
                };

                bets.Add(new Bet(selectedType, amount, selection));
            }

            return bets;
        }
    }

    class ProgramRouletteGame2
    {
        public static void Main()
        {
            // Run RTP simulation
            var simulator = new RtpSimulator();
            simulator.RunSimulation();
        }
    }
}