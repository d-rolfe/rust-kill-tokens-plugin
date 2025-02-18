using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using static ProtoBuf.NeonSign;

// TODO:
// 1. OK: refactor code so that tokencount is a standalone thing that can get called whenever
// 2. OK: roundend can get called by admin to end the round
// 3. OK: also track lifetime tokens, which persists between rounds
// 4. OK: also track actual kill counts
// 5. OK: review ui code if best practice
// 6. OK: make UI cleaner

namespace Oxide.Plugins // Oxide.Plugins OR Carbon.Plugins
{
    [Info("Kill Tokens Plugin", "Daemonus", "1.0.0")]
    [Description("Keep track of player kills with a token system")]
    public class KillTokens : CovalencePlugin  // : CovalencePlugin OR CarbonPlugin
    {
        #region global variables

        public static string tokenShortName = "whiteidtag";
        public static int tokenItemId = 22947882;
        public static int tokenRewardAmount = 1;
        private static string UIName = "KTUI_Main";
        private TokenData tokenData = null;
        private string dataFileName = ".\\carbon\\data\\KillTokensDataFile.json";
        //private string dataFileName = ".\\oxide\\data\\KillTokensDataFile.json";

        #endregion global variables

        #region classes

        private class PlayerInfo
        {
            public string Id;
            public string Name;
            public int TokenCount;
            public int RoundsWon;
            public int TotalTokenCount;
            public int TotalKills;

            public PlayerInfo()
            {
            }

            public PlayerInfo(string id, string name, int tokenCount, int roundsWon, int totalTokenCount, int totalKills)
            {
                this.Id = id;
                this.Name = name;
                this.TokenCount = tokenCount;
                this.RoundsWon = roundsWon;
                this.TotalTokenCount = totalTokenCount;
                this.TotalKills = totalKills;
            }
        }

        private class TokenData
        {
            public IDictionary<string, PlayerInfo> Players = new Dictionary<string, PlayerInfo>();

            public TokenData()
            {
            }
        }

        #endregion classes

        #region functions

        private void saveData()
        {
            string dataToSave = JsonConvert.SerializeObject(tokenData, Formatting.Indented);
            try
            {
                File.WriteAllText(dataFileName, dataToSave);
            } catch (Exception e)
            {
                Puts("Error saving data to file: " + e.Message);
            }
        }

        private void handleNewTokenCounted(Item item, string playerId)
        {
            if (item.ToString().Contains(tokenShortName))  // redundant check?, we already know its a whiteidtag
            {
                Puts("Found: " + item.ToString() + " in player inventory/box.");
                tokenData.Players[playerId].TokenCount += item.amount;
                tokenData.Players[playerId].TotalTokenCount += item.amount;
                item.RemoveFromContainer();
                item.Remove(0f);
            }
        }

        private void countTokensInPlayerInventories()
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                var player = BasePlayer.activePlayerList[i];
                var items = player?.inventory?.FindItemsByItemID(tokenItemId) ?? null;

                if (items != null && items.Count > 0)
                {
                    for (int j = 0; j < items.Count; j++)
                    {
                        var item = items[j];
                        handleNewTokenCounted(item, player.userID.ToString());
                    }
                }
            }
        }

        private void countTokensInSleepingPlayerInventories()
        {
            for (int i = 0; i < BasePlayer.sleepingPlayerList.Count; i++)
            {
                var player = BasePlayer.sleepingPlayerList[i];
                var items = player?.inventory?.FindItemsByItemID(tokenItemId) ?? null;
                if (items != null && items.Count > 0)
                {
                    for (int j = 0; j < items.Count; j++)
                    {
                        var item = items[j];
                        handleNewTokenCounted(item, player.userID.ToString());
                    }
                }
            }
        }

        private void countTokensInPlayerBoxes()
        {
            foreach (var box in GameObject.FindObjectsOfType<StorageContainer>())
            {
                if (box.name.Contains("box"))
                {
                    //foreach (var p in BasePlayer.activePlayerList) // can change to tokenData.Players, or will also need to look through sleeping/offline players
                    foreach (var entry in tokenData.Players)
                    {
                        if (entry.Value.Id.Contains(box.OwnerID.ToString()))
                        {
                            var items = box.inventory?.itemList ?? null;
                            if (items != null)
                            {
                                for (int j = 0; j < items.Count; j++)
                                {
                                    var item = items[j];
                                    handleNewTokenCounted(item, box.OwnerID.ToString());
                                }
                            }
                        }
                    }
                }
            }
        }

        private List<PlayerInfo> GetSortedTokenScores()
        {
            List<PlayerInfo> result = new List<PlayerInfo>();
            IDictionary<string, PlayerInfo> copyOfPlayersDict = new Dictionary<string, PlayerInfo>(this.tokenData.Players);
            int currentMaxTokenVal = 0;
            string currentMaxKey = null;

            while (copyOfPlayersDict.Count > 0)
            {
                foreach (KeyValuePair<string, PlayerInfo> entry in copyOfPlayersDict)
                {
                    if (entry.Value.TokenCount >= currentMaxTokenVal)
                    {
                        currentMaxTokenVal = entry.Value.TokenCount;
                        currentMaxKey = entry.Key;
                    }
                }
                string key = currentMaxKey;
                PlayerInfo val = copyOfPlayersDict[key];
                result.Add(val);
                copyOfPlayersDict.Remove(key);
                currentMaxKey = null;
                currentMaxTokenVal = 0;
            }
            return result;
        }

        private void showTokenCountsInConsole()
        {
            foreach (KeyValuePair<string, PlayerInfo> entry in tokenData.Players)
            {
                Puts("Key: " + entry.Key + " | Value: " + entry.Value.TokenCount);
            }
        }

        private void broadcastMessage(string message)
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (!string.IsNullOrEmpty(message))
                {
                    p.ChatMessage(message);
                }
            }
        }

        private void showScoreUI(List<PlayerInfo> sortedScores)
        {
            CuiElementContainer container = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = { Color = $"1 1 1 0.5" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        CursorEnabled = true
                    },
                    new CuiElement().Parent = "Overlay",
                    UIName
                }
            };

            container.Add(new CuiPanel
            {
                Image = { Color = $"0.4 0.5 0.6 0.9" },
                RectTransform = { AnchorMin = "0.005 0.93", AnchorMax = "0.995 0.99" },
                CursorEnabled = true
            },
            UIName);

            container.Add(new CuiLabel
            {
                Text = {
                    FontSize = 22,
                    Align = TextAnchor.MiddleLeft,
                    FadeIn = 0f,
                    Text = $"<color=blue>Kill Tokens UI || Version 1.0.0</color>" },
                RectTransform = { AnchorMin = "0.05 0.93", AnchorMax = "0.6 0.99" }
            },
            UIName);

            //float dimension = 0.19f;
            string rankHeader = "RANK".PadRight(5);
            string nameHeader = "NAME".PadRight(30);
            string tokensHeader = "TOKENS".PadRight(6);
            string totalTokensHeader = "TOTAL TOKENS".PadRight(12);
            string totalKillsHeader = "TOTAL KILLS".PadRight(11);
            string roundsWonHeader = "ROUNDS WON".PadRight(10);

            string rank;
            string name;
            string tokens;
            string totalTokens;
            string totalKills;
            string roundsWon;

            for (int i = 0; i < sortedScores.Count; i++)
            {
                float left = 0.005f;
                float bottom = 0.01f;
                float right = 0.92f;
                float top = 0.92f;
                float rowHeight = 0.05f;

                if (i == 0)
                {
                    // add column headers
                    container.Add(new CuiLabel
                    {
                        Text = {
                            FontSize = 22,
                            Align = TextAnchor.UpperLeft,
                            FadeIn = 0f,
                            Text = $"{rankHeader} | {nameHeader} | {tokensHeader} | {totalTokensHeader} | {roundsWonHeader} | {totalKillsHeader}"
                        },
                        RectTransform = { AnchorMin = $"{left} {bottom - (i * rowHeight)}", AnchorMax = $"{right} {top - (i * rowHeight)}" }
                    },
                    UIName);
                }

                // print row data
                rank = (i + 1) + ".".ToString().PadRight(10);
                name = sortedScores[i].Name.PadRight(36);
                tokens = sortedScores[i].TokenCount.ToString().PadRight(20);
                totalTokens = sortedScores[i].TotalTokenCount.ToString().PadRight(25);
                roundsWon = sortedScores[i].RoundsWon.ToString().PadRight(22);
                totalKills = sortedScores[i].TotalKills.ToString().PadRight(30);
                
                container.Add(new CuiLabel
                {
                    Text = {
                        FontSize = 22,
                        Align = TextAnchor.UpperLeft,
                        FadeIn = 0f,
                        Text = $"{rank} {name} {tokens} {totalTokens} {roundsWon} {totalKills}"
                    },
                    RectTransform = { AnchorMin = $"{left} {bottom - ((i+1) * rowHeight)}", AnchorMax = $"{right} {top - ((i+1) * rowHeight)}" }
                },
                UIName);
            }

            container.Add(new CuiButton
            {
                Button = { Color = $"0 0 0 1", Command = "KTUI_DestroyAll", FadeIn = 0f },
                RectTransform = { AnchorMin = "0.85 0.94", AnchorMax = "0.95 0.98" },
                Text = { Text = "close", FontSize = 16, Align = TextAnchor.MiddleCenter }
            },
            UIName);

            foreach (var p in BasePlayer.activePlayerList)
            {
                CuiHelper.AddUi(p, container);
            }
        }

        #endregion function

        #region hooks

        private void Init()
        {
            Puts("Kill Token system initializing!");

            tokenData = new TokenData();

            if (File.Exists(dataFileName))
            {
                // load data
                string filetext = File.ReadAllText(dataFileName);
                try
                {
                    TokenData data = JsonConvert.DeserializeObject<TokenData>(filetext);
                    if (data != null)
                    {
                        tokenData = data;
                    } else
                    {
                        Puts("Error loading data from file: data was null.");
                    }
                } catch (Exception e)
                {
                    Puts("Error loading data from file: " + e.Message);
                }
            }
            else
            {
                // create new file
                saveData();
            }
        }

        void OnUserConnected(IPlayer player) // TODO: update to Carbon hook
        {
            if (!tokenData.Players.ContainsKey(player.Id))
            {
                PlayerInfo playerInfo = new PlayerInfo(
                    player.Id,
                    player.Name,
                    0,
                    0,
                    0,
                    0
                );
                tokenData.Players.Add(player.Id, playerInfo);
                saveData();
                Puts("Added player " + player.Id + " to the token count dictionary.");
            }
        }

        private void Loaded()
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                string playerId = p.userID.ToString();
                string playerName = p.displayName;
                if (!tokenData.Players.ContainsKey(playerId))
                {
                    // player connected before plugin init, so is not in players dict, so add
                    PlayerInfo playerInfo = new PlayerInfo(
                        playerId,
                        playerName,
                        0,
                        0,
                        0,
                        0
                    );
                    tokenData.Players.Add(playerId, playerInfo);
                    saveData();
                    Puts("Added player " + playerId + " to the token count dictionary.");
                }
            }
        }

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            string Id = player.userID.ToString();
            string attackerId = null;
            if ((info != null) && (info.InitiatorPlayer != null))
            {
                attackerId = info.InitiatorPlayer.userID.ToString();
                Puts("Player was killed by " + attackerId);
                // if (attackerId != Id && (tokenData.Players.ContainsKey(attackerId)))  // or !player is NPCPlayer
                if ((tokenData.Players.ContainsKey(attackerId))) // TODO: change back to above after testing with f1kill
                {
                    server.Command(string.Format("inventory.giveto {0} {1} {2}", Id, tokenShortName, tokenRewardAmount));
                    tokenData.Players[Id].TotalKills += 1;
                }
            }
            // server.Command(string.Format("inventory.giveto {0} {1} {2}", Id, tokenShortName, tokenRewardAmount));

            return null;
        }

        // only applies to picking item up from ground
        object OnItemPickup(Item item, BasePlayer player)
        {
            Puts("onitempickup");
            if (item.ToString().Contains(tokenShortName))
            {
                string message = string.Format($"{0} just picked up {1} token(s)!", player.displayName, item.amount);
                broadcastMessage(message);
            }
            return null;
        }

        // looting a body
        void OnLootItem(PlayerLoot playerLoot, Item item)
        {
            Puts("onLootItem");
            if (item.ToString().Contains(tokenShortName))
            {
                string message = string.Format($"{0} just picked up {1} token(s)!", "somebody", item.amount);
                broadcastMessage(message);
            }
        }

        // TODO: need hook for taking item from box into inventory, not included in above

        void OnServerSave()
        {
            Puts("OnServerSave was called!");
            saveData();
        }

        #endregion hooks

        #region commands

        [Command("showcounts")]  // only displays in server cmd terminal
        private void ShowTokenCounts(IPlayer player)
        {
            foreach (KeyValuePair<string, PlayerInfo> entry in tokenData.Players)
            {
                Puts("Key: " + entry.Key + "Name: " + entry.Value.Name + " | Value: " + entry.Value.TokenCount);
            }
        }

        [Command("KTUI_DestroyAll")]
        private void KTUI_DestroyAll(IPlayer player)
        {
            if (player == null) return;
            BasePlayer basePlayer = player.Object as BasePlayer;
            CuiHelper.DestroyUi(basePlayer, UIName);
        }

        private void CountTokensHelper()
        {
            countTokensInPlayerInventories();
            countTokensInPlayerBoxes();
            countTokensInSleepingPlayerInventories();
            saveData();
        }

        [Command("counttokens")]
        private void CountTokens(IPlayer player)
        {
            if (player == null) return;
            if (!player.IsAdmin) return;
            CountTokensHelper();
            List<PlayerInfo> sortedScores = GetSortedTokenScores();
            showScoreUI(sortedScores);
        }

        private void GetRoundWinner(List<PlayerInfo> sortedScores)
        {
            tokenData.Players[sortedScores[0].Id].RoundsWon += 1;
            foreach (KeyValuePair<string, PlayerInfo> entry in tokenData.Players)
            {
                tokenData.Players[entry.Key].TokenCount = 0;
            }
            saveData();
        }

        [Command("endround")]
        private void EndRound(IPlayer player)
        {
            if (player == null) return;
            if (!player.IsAdmin) return;
            CountTokensHelper();
            List<PlayerInfo> sortedScores = GetSortedTokenScores();
            GetRoundWinner(sortedScores);
            showScoreUI(sortedScores);
        }
        #endregion commands
    }

    public class program
    {
        public static void Main(String[] args)
        {
            Console.WriteLine("Ran program");
        }
    }
}
