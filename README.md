# rust-kill-tokens-plugin
a server plugin for the PC game Rust that tracks pvp stats

# OVERVIEW
When a player is killed in pvp a white keycard is spawned on their body. This keycard is lootable by any player. This plugin will keep score of how many keycards each player has which is defined as:
- keycards in the player's inventory
- keycards in any container owned by the player

The plugin will also track the total number of kills a player has.
Scores are displayed in a UI.

# PREREQUESITES
Have a rust game server installed and either Carbon or Oxide installed.

# INSTALL
Simply drag and drop the KillTokens.cs file into your plugins folder, usually <server_root>/rustds/carbon/plugins/ or <server_root>/rustds/oxide/plugins/ .

# CHAT COMMANDS
- type /counttokens to show the UI that displays, for all players, current number of tokens (this round), total lifetime tokens, rounds won, and total lifetime kills
- type /endround to finish the round and determine the round winner, and display the count tokens UI
  
