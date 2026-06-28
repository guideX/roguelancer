using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Represents an NPC character found in the bar who can offer missions
    /// </summary>
    public class BarNpc
    {
        public string Name { get; }
        public string Title { get; }
        public string[] GreetingLines { get; }
        public string[] MissionOfferLines { get; }
        public string[] DeclineLines { get; }
        public string FactionId { get; }
        public Mission CurrentMission { get; set; }

        public BarNpc(string name, string title, string[] greetingLines, string[] missionOfferLines, string[] declineLines, string factionId = null)
        {
            Name = name;
            Title = title;
            GreetingLines = greetingLines;
            MissionOfferLines = missionOfferLines;
            DeclineLines = declineLines;
            FactionId = FactionManager.NormalizeFactionId(factionId);
        }

        /// <summary>
        /// Get a random greeting line
        /// </summary>
        public string GetGreeting()
        {
            var rng = new Random();
            return GreetingLines[rng.Next(GreetingLines.Length)];
        }

        /// <summary>
        /// Get a random mission offer line
        /// </summary>
        public string GetMissionOfferLine()
        {
            var rng = new Random();
            return MissionOfferLines[rng.Next(MissionOfferLines.Length)];
        }

        /// <summary>
        /// Get a random decline response
        /// </summary>
        public string GetDeclineLine()
        {
            var rng = new Random();
            return DeclineLines[rng.Next(DeclineLines.Length)];
        }

        /// <summary>
        /// Generate a list of bar NPCs for a station
        /// </summary>
        public static List<BarNpc> GenerateBarNpcs()
        {
            return new List<BarNpc>
            {
                new BarNpc(
                    "Marcus Cole",
                    "Freelance Pilot",
                    new[] { "Hey there, pilot. Pull up a chair.", "You look like you could use some work.", "The lanes aren't safe these days." },
                    new[] { "I've got a job if you're interested.", "This one pays well, but it's not easy.", "A contact of mine needs something done." },
                    new[] { "Your loss, friend.", "Maybe next time.", "I'll find someone else then." },
                    FactionManager.NeutralCivilians
                ),
                new BarNpc(
                    "Elena Vasquez",
                    "Trade Union Rep",
                    new[] { "Welcome, freelancer.", "The economy's been rough lately.", "I represent some important clients." },
                    new[] { "I have a contract that needs filling.", "This delivery is time-sensitive.", "Can I count on you for this?" },
                    new[] { "I understand. Safety first.", "Perhaps another time.", "I'll keep the offer open." },
                    FactionManager.LibertyCorporations
                ),
                new BarNpc(
                    "Rex \"Ironjaw\" Torren",
                    "Bounty Hunter",
                    new[] { "Well, well. Another gun for hire.", "You any good in a fight?", "I've seen better pilots... and worse." },
                    new[] { "Got a mark that needs to disappear.", "This target has a bounty on their head.", "Think you can handle a real fight?" },
                    new[] { "Didn't think so.", "Come back when you grow a spine.", "Fine. More credits for me." },
                    FactionManager.BountyHunters
                ),
                new BarNpc(
                    "Dr. Yun Nakamura",
                    "Research Scientist",
                    new[] { "Ah, a spacer. Interesting.", "I don't usually talk to pilots.", "My work requires... discretion." },
                    new[] { "I need someone to escort a research vessel.", "This is sensitive cargo. Handle with care.", "The pay is good. The risks... manageable." },
                    new[] { "I see. I'll find another way.", "A shame. The data is quite valuable.", "Very well. Good day." },
                    FactionManager.NeutralCivilians
                ),
                new BarNpc(
                    "Zara \"Six-Shot\" Mendez",
                    "Rogue Operative",
                    new[] { "Keep your voice down.", "Don't ask questions you don't want answered.", "You look like someone who can keep a secret." },
                    new[] { "I need a pilot who doesn't ask questions.", "This job is off the books.", "Big money, no questions. Interested?" },
                    new[] { "Smart move. Or dumb. We'll see.", "Walk away then.", "Forget you ever saw me." },
                    FactionManager.LibertyRogues
                )
            };
        }
    }
}
