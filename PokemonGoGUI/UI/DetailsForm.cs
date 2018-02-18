using BrightIdeasSoftware;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Settings.Master;
using PokemonGoGUI.Enums;
using PokemonGoGUI.Extensions;
using PokemonGoGUI.GoManager;
using PokemonGoGUI.GoManager.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PokemonGoGUI.UI
{
    public partial class DetailsForm : System.Windows.Forms.Form
    {
        private Manager _manager;
        private int _totalLogs = 0;

        public DetailsForm(Manager manager)
        {
            InitializeComponent();

            _manager = manager;

            fastObjectListViewLogs.PrimarySortColumn = olvColumnDate;
            fastObjectListViewLogs.PrimarySortOrder = SortOrder.Descending;
            fastObjectListViewLogs.ListFilter = new TailFilter(500);

            fastObjectListViewPokedex.PrimarySortColumn = olvColumnPokedexFriendlyName;
            fastObjectListViewPokedex.PrimarySortOrder = SortOrder.Ascending;

            fastObjectListViewLogs.BackColor = Color.FromArgb(43, 43, 43);

            fastObjectListViewPokedex.BackColor = Color.FromArgb(43, 43, 43);
            fastObjectListViewPokedex.ForeColor = Color.LightGray;

            fastObjectListViewPokemon.BackColor = Color.FromArgb(43, 43, 43);
            fastObjectListViewPokemon.ForeColor = Color.LightGray;

            fastObjectListViewInventory.BackColor = Color.FromArgb(43, 43, 43);
            fastObjectListViewInventory.ForeColor = Color.LightGray;

            fastObjectListViewEggs.BackColor = Color.FromArgb(43, 43, 43);
            fastObjectListViewEggs.ForeColor = Color.LightGray;

            fastObjectListViewCandy.BackColor = Color.FromArgb(43, 43, 43);
            fastObjectListViewCandy.ForeColor = Color.LightGray;

            #region Pokedex

            //ToString for sorting purposes
            olvColumnPokedexFriendlyName.AspectGetter = (entry) => (int)(entry as PokedexEntry).PokemonId;

            olvColumnPokedexId.AspectGetter = (entry) => (entry as PokedexEntry).PokemonId.ToString();

            olvColumnPokedexFriendlyName.AspectGetter = (entry) => (int)(entry as PokedexEntry).PokemonId;

            #endregion

            #region Pokemon


            olvColumnPokemonId.AspectGetter = (pokemon) => (int)(pokemon as PokemonData).PokemonId;

            olvColumnPokemonFavorite.AspectGetter = (pokemon) => (pokemon as PokemonData).Favorite == 1; 

            olvColumnPokemonShiny.AspectGetter = (pokemon) => (pokemon as PokemonData).PokemonDisplay.Shiny;

            olvColumnPokemonGender.AspectGetter = (pokemon) => (pokemon as PokemonData).PokemonDisplay.Gender;

            olvColumnPokemonRarity.AspectGetter = delegate (object pokemon)
            {
                PokemonSettings pokemonSettings = _manager.GetPokemonSetting((pokemon as PokemonData).PokemonId).Data;
                return pokemonSettings == null ? PokemonRarity.Normal : pokemonSettings.Rarity;
            };

            olvColumnCandyToEvolve.AspectGetter = delegate (object pokemon)
            {
                PokemonSettings pokemonSettings = _manager.GetPokemonSetting((pokemon as PokemonData).PokemonId).Data;
                return pokemonSettings == null ? 0 : pokemonSettings.EvolutionBranch.Select(x => x.CandyCost).FirstOrDefault();
            };

            olvColumnPokemonCandy.AspectGetter = delegate (object pokemon)
            {
                if (!_manager.PokemonCandy.Any())
                {
                    return 0;
                }

                PokemonSettings settings = _manager.GetPokemonSetting((pokemon as PokemonData).PokemonId).Data;

                if (settings == null)
                {
                    return 0;
                }

                Candy family = _manager.PokemonCandy.FirstOrDefault(y => y.FamilyId == settings.FamilyId);

                return family == null ? 0 : family.Candy_;
            };

            olvColumnPokemonName.AspectGetter = delegate (object pokemon)
            {
               return String.IsNullOrEmpty((pokemon as PokemonData).Nickname) ? (pokemon as PokemonData).PokemonId.ToString() : (pokemon as PokemonData).Nickname;
            };

            olvColumnPrimaryMove.AspectGetter = (pokemon) => ((PokemonMove)(pokemon as PokemonData).Move1).ToString().Replace("Fast", "");

            olvColumnSecondaryMove.AspectGetter = (pokemon) => ((PokemonMove)(pokemon as PokemonData).Move2).ToString();

            olvColumnAttack.AspectGetter = (pokemon) => (pokemon as PokemonData).IndividualAttack;
            olvColumnDefense.AspectGetter = (pokemon) => (pokemon as PokemonData).IndividualDefense;
            olvColumnStamina.AspectGetter = (pokemon) => (pokemon as PokemonData).IndividualStamina;


            olvColumnPerfectPercent.AspectGetter = delegate (object pokemon)
            {
                double settings = Manager.CalculateIVPerfection(pokemon as PokemonData);
                string sDouble = String.Format("{0:0.00}", settings);
                return double.Parse(sDouble);
            };

            olvColumnPokemonHeight.AspectGetter = delegate (object pokemon)
            {
                return String.Format("{0:0.00}m", (pokemon as PokemonData).HeightM);
            };

            olvColumnPokemonWeight.AspectGetter = delegate (object pokemon)
            {
                return String.Format("{0:0.00}Kg", (pokemon as PokemonData).WeightKg);
            };

            #endregion

            #region Candy

            olvColumnCandyFamily.AspectGetter = delegate (object x)
            {
                var family = (Candy)x;

                return family.FamilyId.ToString().Replace("Family", "");
            };

            #endregion

            #region Inventory

            olvColumnInventoryItem.AspectGetter = delegate (object x)
            {
                var item = (ItemData)x;

                return item.ItemId.ToString().Replace("Item", "");
            };

            #endregion
        }

        private void DetailsForm_Load(object sender, EventArgs e)
        {
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            _totalLogs = _manager.Logs.Count;

            if (_manager.LogHeaderSettings != null)
            {
                fastObjectListViewLogs.RestoreState(_manager.LogHeaderSettings);
            }

            fastObjectListViewLogs.SetObjects(_manager.Logs);

            var values = new List<LoggerTypes>();

            foreach (LoggerTypes type in Enum.GetValues(typeof(LoggerTypes)))
            {
                if (type == LoggerTypes.LocationUpdate)
                {
                    continue;
                }

                values.Add(type);
            }

            olvColumnStatus.ValuesChosenForFiltering = values;
            fastObjectListViewLogs.UpdateColumnFiltering();

            Text = _manager.AccountName;

            _manager.OnLog += _manager_OnLog;

            DisplayDetails();
        }

        private void _manager_OnLog(object sender, LoggerEventArgs e)
        {
            if (!_manager.IsRunning)
                return;
                
            if (fastObjectListViewLogs.IsDisposed || fastObjectListViewLogs.Disposing)
            {
                return;
            }

            fastObjectListViewLogs.SetObjects(_manager.Logs);

            DisplayDetails();

            if (e.LogType != LoggerTypes.LocationUpdate)
            {
                Invoke(new MethodInvoker(() =>
                {
                    if (tabControlMain.SelectedTab == tabPageLogs)
                    {
                        _totalLogs = _manager.TotalLogs;
                    }
                    else
                    {
                        int newLogs = _manager.TotalLogs - _totalLogs;

                        if (newLogs > 0)
                        {
                            tabPageLogs.Text = String.Format("Logs ({0})", newLogs);
                        }
                    }
                }));
            }
        }

        private void DetailsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _manager.OnLog -= _manager_OnLog;

            _manager.LogHeaderSettings = fastObjectListViewLogs.SaveState();
        }

        private void DisplayDetails()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(DisplayDetails));

                return;
            }

            labelPlayerLevel.Text = _manager.Level.ToString();
            labelPlayerTeam.Text = !String.IsNullOrEmpty(_manager.Team) ? _manager.Team : "Neutral";
            labelExp.Text = _manager.ExpRatio.ToString();
            labelRunningTime.Text = _manager.RunningTime;
            labelStardust.Text = _manager.TotalStardust.ToString();
            labelExpPerHour.Text = String.Format("{0:0}", _manager.ExpPerHour);
            labelExpGained.Text = _manager.ExpGained.ToString();
            labelPokeCoins.Text = _manager.TotalPokeCoins.ToString();

            if (_manager.Stats != null)
            {
                labelDistanceWalked.Text = String.Format("{0:0.00}km", _manager.Stats.KmWalked);
                labelPokemonCaught.Text = _manager.Stats.PokemonsCaptured.ToString();
                labelPokestopVisits.Text = _manager.Stats.PokeStopVisits.ToString();
                labelUniquePokemon.Text = _manager.Stats.UniquePokedexEntries.ToString();
            }

            if (_manager.Pokemon != null)
            {
                labelPokemonCount.Text = String.Format("{0}/{1}", _manager.Pokemon.Count + _manager.Eggs.Count, _manager.MaxPokemonStorage);
                labelDeployedPokemons.Text = _manager.Pokemon.Where(i => !string.IsNullOrEmpty(i.DeployedFortId)).Count().ToString();
            }

            if (_manager.Items != null)
            {
                labelInventoryCount.Text = String.Format("{0}/{1}", _manager.Items.Sum(x => x.Count), _manager.MaxItemStorage);
            }

            if (_manager.PlayerData != null)
            {
                BuddyPokemon buddy = _manager.PlayerData.BuddyPokemon ?? new BuddyPokemon();
                PokemonData myBuddy = _manager.Pokemon.Where(x => x.Id == buddy.Id).FirstOrDefault() ?? new PokemonData();
                labelPokemonBuddy.Text = myBuddy.PokemonId != PokemonId.Missingno ? String.Format("{0}", myBuddy.PokemonId) : "Not set";
                labelPlayerUsername.Text = _manager.PlayerData.Username;
                DateTime date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(_manager.PlayerData.CreationTimestampMs);
                labelCreateDate.Text = date.ToString();
                string tutocompleted = "Not Completed";
                if (_manager.PlayerData.TutorialState.Contains(TutorialState.AccountCreation)
                    && _manager.PlayerData.TutorialState.Contains(TutorialState.AvatarSelection)
                    && _manager.PlayerData.TutorialState.Contains(TutorialState.FirstTimeExperienceComplete)
                    && _manager.PlayerData.TutorialState.Contains(TutorialState.GymTutorial)
                    && _manager.PlayerData.TutorialState.Contains(TutorialState.LegalScreen)
                    && _manager.PlayerData.TutorialState.Contains(TutorialState.NameSelection)
                    && _manager.PlayerData.TutorialState.Contains(TutorialState.PokemonBerry)
                    && _manager.PlayerData.TutorialState.Contains(TutorialState.PokemonCapture)
                    && _manager.PlayerData.TutorialState.Contains(TutorialState.PokestopTutorial)
                    && _manager.PlayerData.TutorialState.Contains(TutorialState.UseItem)
                    )
                    tutocompleted = "Completed";
                labelTutorialState.Text = tutocompleted;
            }
        }

        private void ButtonUpdateStats_Click(object sender, EventArgs e)
        {
            DisplayDetails();
        }

        private void FastObjectListViewLogs_FormatRow(object sender, FormatRowEventArgs e)
        {
            var log = e.Model as Log;

            if (log == null)
            {
                return;
            }

            e.Item.ForeColor = log.GetLogColor();
        }

        private void FastObjectListViewPokemon_FormatCell(object sender, FormatCellEventArgs e)
        {
            var pokemonData = (PokemonData)e.Model;

            if (pokemonData == null)
                return;

            if (e.Column == olvColumnPokemonName)
            {
                bool fav = (bool)olvColumnPokemonFavorite.GetValue(pokemonData);
                bool bubby = _manager?.PlayerData?.BuddyPokemon?.Id == pokemonData.Id == true;
                if (fav)
                {
                    e.SubItem.ForeColor = Color.Gold;
                }
                else if (bubby)
                {
                    e.SubItem.ForeColor = Color.Blue;
                }
            }
            else if (e.Column == olvColumnPokemonCandy)
            {
                int candy = (int)olvColumnPokemonCandy.GetValue(pokemonData);
                int candyToEvolve = (int)olvColumnCandyToEvolve.GetValue(pokemonData);

                if (candyToEvolve > 0)
                {
                    e.SubItem.ForeColor = candy >= candyToEvolve ? Color.Green : Color.Red;
                }
            }
            else if (e.Column == olvColumnPerfectPercent)
            {
                double perfectPercent = Convert.ToDouble(olvColumnPerfectPercent.GetValue(pokemonData));

                if (perfectPercent >= 93)
                {
                    e.SubItem.ForeColor = Color.Green;
                }
                else if (perfectPercent >= 86)
                {
                    e.SubItem.ForeColor = Color.Orange;
                }
                else
                {
                    e.SubItem.ForeColor = Color.Red;
                }
            }
            else if (e.Column == olvColumnAttack)
            {
                if (pokemonData.IndividualAttack >= 13)
                {
                    e.SubItem.ForeColor = Color.Green;
                }
                else if (pokemonData.IndividualAttack >= 11)
                {
                    e.SubItem.ForeColor = Color.Yellow;
                }
                else if (pokemonData.IndividualAttack >= 9)
                {
                    e.SubItem.ForeColor = Color.Orange;
                }
                else
                {
                    e.SubItem.ForeColor = Color.Red;
                }

            }
            else if (e.Column == olvColumnDefense)
            {
                if (pokemonData.IndividualDefense >= 13)
                {
                    e.SubItem.ForeColor = Color.Green;
                }
                else if (pokemonData.IndividualDefense >= 11)
                {
                    e.SubItem.ForeColor = Color.Yellow;
                }
                else if (pokemonData.IndividualDefense >= 9)
                {
                    e.SubItem.ForeColor = Color.Orange;
                }
                else
                {
                    e.SubItem.ForeColor = Color.Red;
                }
            }
            else if (e.Column == olvColumnStamina)
            {
                if (pokemonData.IndividualStamina >= 13)
                {
                    e.SubItem.ForeColor = Color.Green;
                }
                else if (pokemonData.IndividualStamina >= 11)
                {
                    e.SubItem.ForeColor = Color.Yellow;
                }
                else if (pokemonData.IndividualStamina >= 9)
                {
                    e.SubItem.ForeColor = Color.Orange;
                }
                else
                {
                    e.SubItem.ForeColor = Color.Red;
                }
            }
        }

        private async void UpgradeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(String.Format("Are you sure you want to upgrade {0} pokemon?", fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>().Count()), "Confirmation", MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
            {
                return;
            }

            if (fastObjectListViewPokemon.SelectedObjects.Count == 0 || fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>().FirstOrDefault() == null)
                return;

            contextMenuStripPokemonDetails.Enabled = false;

            MethodResult managerResult = await _manager.UpgradePokemon(fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>());

            DisplayDetails();

            contextMenuStripPokemonDetails.Enabled = true;

            fastObjectListViewPokemon.SetObjects(_manager.Pokemon);

            MessageBox.Show("Finished upgrade pokemon");
        }

        private async void TransferToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(String.Format("Are you sure you want to transfer {0} pokemon?", fastObjectListViewPokemon.SelectedObjects.Count), "Confirmation", MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
            {
                return;
            }

            if (fastObjectListViewPokemon.SelectedObjects.Count == 0 || fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>().FirstOrDefault() == null)
                return;

            contextMenuStripPokemonDetails.Enabled = false;

            MethodResult managerResult = await _manager.TransferPokemon(fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>());

            DisplayDetails();

            contextMenuStripPokemonDetails.Enabled = true;

            fastObjectListViewPokemon.SetObjects(_manager.Pokemon);

            MessageBox.Show("Finished transferring pokemon");
        }

        private async void EvolveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(String.Format("Are you sure you want to evolve {0} pokemon?", fastObjectListViewPokemon.SelectedObjects.Count), "Confirmation", MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
            {
                return;
            }

            if (fastObjectListViewPokemon.SelectedObjects.Count == 0 || fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>().FirstOrDefault() == null)
                return;

            contextMenuStripPokemonDetails.Enabled = false;

            await _manager.EvolvePokemon(fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>());

            DisplayDetails();

            contextMenuStripPokemonDetails.Enabled = true;

            fastObjectListViewPokemon.SetObjects(_manager.Pokemon);

            MessageBox.Show("Finished evolving pokemon");
        }

        private void TabControlMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControlMain.SelectedTab == tabPageLogs)
            {
                _totalLogs = _manager.TotalLogs;
                tabPageLogs.Text = "Logs";
            }
            else if (tabControlMain.SelectedTab == tabPagePokemon)
            {
                _manager.UpdateInventory(InventoryRefresh.Pokemon);
                fastObjectListViewPokemon.SetObjects(_manager.Pokemon);
            }
            else if (tabControlMain.SelectedTab == tabPageCandy)
            {
                _manager.UpdateInventory(InventoryRefresh.PokemonCandy);
                fastObjectListViewCandy.SetObjects(_manager.PokemonCandy);
            }
            else if (tabControlMain.SelectedTab == tabPageEggs)
            {
                _manager.UpdateInventory(InventoryRefresh.Eggs);
                _manager.UpdateInventory(InventoryRefresh.Incubators);
                fastObjectListViewEggs.SetObjects(_manager.Eggs);
            }
            else if (tabControlMain.SelectedTab == tabPageInventory)
            {
                _manager.UpdateInventory(InventoryRefresh.Items);
                fastObjectListViewInventory.SetObjects(_manager.Items);
            }
            else if (tabControlMain.SelectedTab == tabPagePokedex)
            {
                _manager.UpdateInventory(InventoryRefresh.Pokedex);
                fastObjectListViewPokedex.SetObjects(_manager.Pokedex);
            }
            else if (tabControlMain.SelectedTab == tabPageStats)
            {
                _manager.UpdateInventory(InventoryRefresh.Stats);
                DisplayDetails();
            }
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int total = fastObjectListViewLogs.SelectedObjects.Count;

            if (total == 0)
            {
                return;
            }

            string copiedMessage = String.Join(Environment.NewLine, fastObjectListViewLogs.SelectedObjects.Cast<Log>().Select(x => x.ToString()));

            Clipboard.SetText(copiedMessage);
        }

        private async void ExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Json Files (*.json)|*.json|All Files (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    MethodResult result = await _manager.ExportLogs(sfd.FileName);

                    if (result.Success)
                    {
                        MessageBox.Show("Logs exported");
                    }
                }
            }
        }

        private async void SetFavoriteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            favoriteToolStripMenuItem.Enabled = false;

            await _manager.FavoritePokemon(fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>(), true);

            DisplayDetails();

            favoriteToolStripMenuItem.Enabled = true;

            fastObjectListViewPokemon.SetObjects(_manager.Pokemon);

            MessageBox.Show("Finished favoriting pokemon");
        }

        private async void SetUnfavoriteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            favoriteToolStripMenuItem.Enabled = false;

            await _manager.FavoritePokemon(fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>(), false);

            DisplayDetails();

            favoriteToolStripMenuItem.Enabled = true;

            fastObjectListViewPokemon.SetObjects(_manager.Pokemon);

            MessageBox.Show("Finished unfavoriting pokemon");
        }

        private async void RecycleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = Prompt.ShowDialog("Amount to recycle", "Set recycle amount");

            int amount;
            if (String.IsNullOrEmpty(data) || !Int32.TryParse(data, out amount) || amount <= 0)
            {
                return;
            }

            foreach (ItemData item in fastObjectListViewInventory.SelectedObjects)
            {
                int toDelete = amount;

                if (amount > item.Count)
                {
                    toDelete = item.Count;
                }

                await _manager.RecycleItem(item, toDelete);

                await Task.Delay(500);
            }

            _manager.UpdateInventory(InventoryRefresh.Items);

            fastObjectListViewInventory.SetObjects(_manager.Items);

            MessageBox.Show("Finished recycling items");
        }

        private void CopyStackTraceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var log = fastObjectListViewLogs.SelectedObject as Log;

            if (log == null || String.IsNullOrEmpty(log.StackTrace))
            {
                return;
            }

            Clipboard.SetText(log.StackTrace);

            MessageBox.Show("Stack trace copied");
        }

        private void ShowFutureTransfersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showFutureTransfersToolStripMenuItem.Enabled = false;

            MethodResult<List<PokemonData>> result = _manager.GetPokemonToTransfer();

            if (result.Success)
            {
                fastObjectListViewPokemon.SetObjects(result.Data);
            }
            else
            {
                MessageBox.Show("Failed to get pokemon to be transfered");
            }

            showFutureTransfersToolStripMenuItem.Enabled = true;
        }

        private void FastObjectListViewEggs_FormatCell(object sender, FormatCellEventArgs e)
        {
            var egg = (PokemonData)e.Model;
            var eggIncubator = new EggIncubator();

            foreach (var inc in _manager.Incubators)
            {
                if (inc.PokemonId == egg.Id)
                    eggIncubator = inc;
            }

            if (e.Column == olvColumnEggWalked)
            {
                if (eggIncubator.PokemonId != 0)
                    e.SubItem.Text = String.Format("{0:0.00} km", _manager.Stats.KmWalked - eggIncubator.StartKmWalked);
                else
                    e.SubItem.Text = "0.00 km";
            }
            else if (e.Column == olvColumnEggDistance)
                e.SubItem.Text = String.Format("{0:0.00}km", egg.EggKmWalkedTarget);
        }

        private async void SetABuddyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>().Count() > 1)
            {
                MessageBox.Show(String.Format("Select one pokemon to set a buddy you have set {0} pokemons.", fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>().Count()), "Information", MessageBoxButtons.OK);

                return;
            }

            DialogResult result = MessageBox.Show(String.Format("Are you sure you want to set a buddy {0} pokemon?", fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>().FirstOrDefault().PokemonId), "Confirmation", MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
            {
                return;
            }

            contextMenuStripPokemonDetails.Enabled = false;

            MethodResult managerResult = await _manager.SetBuddyPokemon(fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>().FirstOrDefault());

            DisplayDetails();

            contextMenuStripPokemonDetails.Enabled = true;

            fastObjectListViewPokemon.SetObjects(_manager.Pokemon);

            MessageBox.Show("Finished set a buddy pokemon");
        }

        private async void RenameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(String.Format("Are you sure you want to rename {0} pokemon(s)?", fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>().Count()), "Confirmation", MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
            {
                return;
            }

            contextMenuStripPokemonDetails.Enabled = false;

            MethodResult managerResult = await _manager.RenamePokemon(fastObjectListViewPokemon.SelectedObjects.Cast<PokemonData>());

            DisplayDetails();

            contextMenuStripPokemonDetails.Enabled = true;

            fastObjectListViewPokemon.SetObjects(_manager.Pokemon);

            MessageBox.Show("Finished to rename pokemon(s)");
        }
    }
}
