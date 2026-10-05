using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ShelteredModManager.ContentPacks;

namespace Manager.Controls
{
    /// <summary>One entry in an item picker: the name people read and the ID the pack stores.</summary>
    public sealed class ItemChoice
    {
        public string Name;
        public string Id;

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Base-game items a content pack can reference. The game resolves these
    /// by its ItemManager.ItemType names, so the ID is the enum name and the
    /// readable name is derived from it. Furniture, shelter parts, quest items
    /// and creature attacks are left out because recipes never use them.
    /// </summary>
    public static class VanillaItems
    {
        private static readonly string[] Ids = new string[]
        {
            "Petrol", "FirstAid", "DuctTape", "Wood", "Metal", "Nails", "Wool", "Spring", "Piping", "Valve",
            "Hinge", "Leather", "Nylon", "Satchel", "AntiRadMedicine", "Record", "Book", "Toy", "Matches", "Coal",
            "Antibiotics", "Valium", "Transistor", "Battery", "Fuse", "Wiring", "CircuitBreaker", "CircuitBoard", "Bulb", "Motor",
            "Switch", "Bucket", "Plastic", "Rope", "Rubber", "Sand", "Limestone", "Cordite", "Magnesium", "Lighter",
            "Lens", "Cement", "NailBomb", "Rucksack", "LargeRucksack", "BulletProofVest_poor", "TripWire", "Binoculars", "MetalDetector", "BulletProofVest_good",
            "BulletProofVest_great", "Seeds", "GasMask", "PoorNailBomb", "ExcellentNailBomb", "PaintCan", "PassionFlower", "StJohnsWort", "YellowJasmine", "OakBark",
            "GingerRoot", "Alfalfa", "AloeVera", "HolyBasil", "Echinacea", "Garlic", "RawHoney", "HomemadeAntibiotics", "HomemadeAntiemetics", "HomemadeAntiRad",
            "HomemadeAntidepressant", "Bandages", "PlasticBottle", "BurnedClothing", "BrokenRadio", "Logs", "GlassJar", "ScrapPile", "MannequinLeg", "MannequinHead",
            "MannequinArm", "BurnedLaptop", "BrokenTV", "Meat", "DesperateMeat", "MetalOre", "Camouflage", "MotionDetector", "LockpickSet", "EmptyPetrolCan",
            "BedRoll", "Tent", "CarrierBag", "ProspectorsKit", "CoffeeBeans", "AdvancedGasMask", "MilitaryRucksack", "Soap", "Ration", "AnimalRepellent",
            "Adrenalin", "AnimalFat", "Rocket_EngineChassis", "Rocket_BoosterCylinder", "Rocket_InjectionCylinder", "Rocket_MainBooster", "Rocket_CoreCompPanel", "Rocket_HeatExPanel", "Rocket_BayCover_A", "Rocket_BayCover_B",
            "AluminiumComposite", "TitaniumAlloy", "ReinforcedValveAssembly", "ReinforcedCarbon", "ThermalTiles", "Coolant", "NickelSuperalloy", "NanoElectronics", "BrokenPistol", "BrokenRifle",
            "BrokenShotgun", "CorrodedBattery", "ExpiredAntibiotics", "Fertilizer", "HackingTerminal", "OldSoap", "DamagedTeddy", "RuinedLeather", "RustyLighter", "RustyNail",
            "UsedBandages", "WormHusk", "BrokenBaseballBat", "BrokenCrowbar", "BrokenHatchet", "BrokenKnife", "BrokenKnuckleDuster", "BrokenPipe", "BrokenRebar", "BrokenWood",
            "Water", "DirtyWater",
            "Tool_Hammer", "Tool_Screwdriver", "Tool_Pliers", "Tool_Handsaw", "Tool_Drill", "Tool_Chisel", "Tool_Nailgun", "Tool_Sander", "Tool_Ratchet", "Tool_Wrench",
            "Weapon_Crowbar", "Weapon_Rebar", "Weapon_SledgeHammer", "Weapon_Hatchet", "Weapon_Knife", "Weapon_Rock", "Weapon_BaseballBat", "Weapon_KnuckleDuster", "Weapon_Pistol", "Weapon_Shotgun",
            "Weapon_Rifle", "Weapon_LuckyLuckyBat", "Weapon_KnuckleDustersOfMight", "Weapon_RebarOfConfidence", "Weapon_FlashKnife", "Weapon_LibrariansCrowbar", "Weapon_LaserPistol", "Weapon_LaserRifle", "Weapon_LaserShotgun", "Weapon_M16",
            "Combat_Frag", "Combat_PipeBomb", "Combat_FlashBang", "Combat_PoorPipeBomb", "Combat_ExcellentPipeBomb", "Combat_PoorFlashBang", "Combat_ExcellentFlashBang", "Combat_Nailbomb",
            "Car_Distributor", "Car_Alternator", "Car_CarBattery", "Car_SparkPlug", "Car_FanBelt", "Car_Tyre", "Car_ClutchCable", "Car_StarterMotor", "Horse_Saddle",
            "Ammo_Pistol", "Ammo_Shotgun", "Ammo_Rifle", "Ammo_Laser"
        };

        private static readonly Dictionary<string, string> NameOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "FirstAid", "First aid kit" },
            { "AntiRadMedicine", "Anti-rad medicine" },
            { "HomemadeAntiRad", "Homemade anti-rad" },
            { "StJohnsWort", "St John's wort" },
            { "BulletProofVest_poor", "Bulletproof vest (poor)" },
            { "BulletProofVest_good", "Bulletproof vest (good)" },
            { "BulletProofVest_great", "Bulletproof vest (great)" }
        };

        private static List<ItemChoice> _all;

        public static IList<ItemChoice> All
        {
            get
            {
                if (_all == null)
                {
                    List<ItemChoice> list = new List<ItemChoice>(Ids.Length);
                    for (int i = 0; i < Ids.Length; i++)
                        list.Add(new ItemChoice { Id = Ids[i], Name = FriendlyName(Ids[i]) });
                    list.Sort(delegate(ItemChoice a, ItemChoice b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
                    _all = list;
                }
                return _all;
            }
        }

        private static string FriendlyName(string id)
        {
            string name;
            if (NameOverrides.TryGetValue(id, out name))
                return name;

            int split = id.IndexOf('_');
            if (split < 0)
                return Words(id);
            string group = id.Substring(0, split);
            string rest = Words(id.Substring(split + 1).Replace('_', ' '));
            switch (group)
            {
                case "Tool": return rest + " (tool)";
                case "Weapon": return rest + " (weapon)";
                case "Combat": return rest + " (throwable)";
                case "Car": return rest + " (car part)";
                case "Rocket": return rest + " (rocket part)";
                case "Horse": return rest + " (horse)";
                case "Ammo": return rest + " ammo";
                default: return Words(id.Replace('_', ' '));
            }
        }

        /// <summary>Splits PascalCase into sentence-case words, keeping acronyms such as TV.</summary>
        private static string Words(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && value[i - 1] != ' ')
                {
                    bool afterLower = char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]);
                    bool endsAcronym = char.IsUpper(value[i - 1]) && i + 1 < value.Length && char.IsLower(value[i + 1]);
                    if (afterLower || endsAcronym)
                        builder.Append(' ');
                }
                builder.Append(c);
            }

            string[] words = builder.ToString().Split(' ');
            for (int w = 1; w < words.Length; w++)
            {
                string word = words[w];
                bool acronym = word.Length > 1 && word.ToUpperInvariant() == word;
                if (!acronym && word.Length > 0)
                    words[w] = char.ToLowerInvariant(word[0]) + word.Substring(1);
            }
            return string.Join(" ", words);
        }
    }

    /// <summary>
    /// Items a pack can point at: its own items first, then base-game items.
    /// Pickers show names; the pack stores IDs. Typed text that matches
    /// nothing is kept as an ID, so references to other mods still work.
    /// </summary>
    public sealed class ItemCatalog
    {
        private readonly List<ItemChoice> _packItems = new List<ItemChoice>();

        public event EventHandler Changed;

        public void SetPackItems(IList<KeyValuePair<string, string>> idsAndNames)
        {
            _packItems.Clear();
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemChoice vanilla in VanillaItems.All)
                usedNames.Add(vanilla.Name);
            if (idsAndNames != null)
            {
                for (int i = 0; i < idsAndNames.Count; i++)
                {
                    string id = (idsAndNames[i].Key ?? string.Empty).Trim();
                    if (id.Length == 0)
                        continue;
                    string name = (idsAndNames[i].Value ?? string.Empty).Trim();
                    if (name.Length == 0)
                        name = id;
                    // Names must round-trip to one ID, so clashes get a qualifier.
                    if (usedNames.Contains(name))
                        name += " (this pack)";
                    if (usedNames.Contains(name))
                        name += " " + id;
                    usedNames.Add(name);
                    _packItems.Add(new ItemChoice { Id = id, Name = name });
                }
            }
            EventHandler handler = Changed;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        public IEnumerable<ItemChoice> Choices
        {
            get
            {
                foreach (ItemChoice choice in _packItems)
                    yield return choice;
                foreach (ItemChoice choice in VanillaItems.All)
                    yield return choice;
            }
        }

        public string NameFor(string id)
        {
            string value = (id ?? string.Empty).Trim();
            if (value.Length == 0)
                return string.Empty;
            foreach (ItemChoice choice in Choices)
                if (string.Equals(choice.Id, value, StringComparison.OrdinalIgnoreCase))
                    return choice.Name;
            return value;
        }

        public string IdFor(string text)
        {
            string value = (text ?? string.Empty).Trim();
            if (value.Length == 0)
                return string.Empty;
            foreach (ItemChoice choice in Choices)
                if (string.Equals(choice.Name, value, StringComparison.OrdinalIgnoreCase))
                    return choice.Id;
            foreach (ItemChoice choice in Choices)
                if (string.Equals(choice.Id, value, StringComparison.OrdinalIgnoreCase))
                    return choice.Id;
            return value;
        }

    }

    /// <summary>
    /// Editable drop-down of item names with type-ahead. Reads and writes IDs
    /// through <see cref="ItemId"/>; anything typed that is not in the list
    /// is kept verbatim as an ID.
    /// </summary>
    public class ItemPicker : ThemedComboBox
    {
        private readonly ItemCatalog _catalog;
        private string _currentId = string.Empty;
        private bool _quiet;

        /// <summary>Raised when the user picks or types a different item.</summary>
        public event EventHandler ItemChanged;

        public ItemPicker(ItemCatalog catalog)
        {
            _catalog = catalog;
            DropDownStyle = ComboBoxStyle.DropDown;
            AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            AutoCompleteSource = AutoCompleteSource.ListItems;
            MaxDropDownItems = 16;
            Font = ManagerTheme.UiFont(9.5f);
            Reload();
            _catalog.Changed += CatalogChanged;
        }

        /// <summary>
        /// The stored item ID. Setting it shows the item's name without raising
        /// <see cref="ItemChanged"/>, and keeps the ID exactly as given.
        /// </summary>
        public string ItemId
        {
            get { return _currentId; }
            set
            {
                ShowQuietly(_catalog.NameFor(value));
                _currentId = (value ?? string.Empty).Trim();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _catalog.Changed -= CatalogChanged;
            base.Dispose(disposing);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (_quiet)
                return;
            string id = _catalog.IdFor(Text);
            if (string.Equals(id, _currentId, StringComparison.Ordinal))
                return;
            _currentId = id;
            EventHandler handler = ItemChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            // Once the user moves on, show the item's proper name for what they typed.
            ShowQuietly(_catalog.NameFor(_currentId));
        }

        private void CatalogChanged(object sender, EventArgs e)
        {
            Reload();
        }

        /// <summary>Rebuilds the name list; an item rename changes the name shown, never the ID.</summary>
        private void Reload()
        {
            _quiet = true;
            try
            {
                BeginUpdate();
                Items.Clear();
                foreach (ItemChoice choice in _catalog.Choices)
                    Items.Add(choice.Name);
                EndUpdate();
                Text = _catalog.NameFor(_currentId);
            }
            finally
            {
                _quiet = false;
            }
        }

        private void ShowQuietly(string text)
        {
            if (string.Equals(Text, text, StringComparison.Ordinal))
                return;
            _quiet = true;
            try
            {
                Text = text;
            }
            finally
            {
                _quiet = false;
            }
        }
    }

    /// <summary>
    /// Editable list of materials (item and amount) with add and remove
    /// buttons, used for recipe ingredients and recycling output.
    /// </summary>
    public class MaterialListEditor : TableLayoutPanel
    {
        private readonly ItemCatalog _catalog;
        private readonly TableLayoutPanel _rows;
        private readonly Label _emptyLabel;
        private readonly ActionButton _addButton;
        private readonly string _noun;
        private int _quiet;

        public event EventHandler ValueChanged;

        public MaterialListEditor(ItemCatalog catalog, string noun)
        {
            _catalog = catalog;
            _noun = string.IsNullOrEmpty(noun) ? "material" : noun;
            ColumnCount = 1;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Dock = DockStyle.Top;
            Margin = new Padding(0);
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            _rows = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0)
            };
            _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _emptyLabel = UiFactory.Text("None yet.", ThemeRole.Muted, 9f, false);
            _emptyLabel.Margin = new Padding(0, 2, 0, 6);
            _addButton = UiFactory.Button("+ Add " + _noun, ActionButtonKind.Secondary, delegate { AddRow(string.Empty, 1, true); });
            _addButton.Margin = new Padding(0, 4, 0, 0);
            _addButton.Anchor = AnchorStyles.Left;

            RowCount = 3;
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(_rows, 0, 0);
            Controls.Add(_emptyLabel, 0, 1);
            Controls.Add(_addButton, 0, 2);
        }

        public List<ContentPackIngredient> Materials
        {
            get
            {
                List<ContentPackIngredient> result = new List<ContentPackIngredient>();
                for (int row = 0; row < _rows.RowCount; row++)
                {
                    ItemPicker picker = _rows.GetControlFromPosition(0, row) as ItemPicker;
                    NumericUpDown count = _rows.GetControlFromPosition(1, row) as NumericUpDown;
                    if (picker == null || count == null)
                        continue;
                    result.Add(new ContentPackIngredient { itemId = picker.ItemId, count = decimal.ToInt32(count.Value) });
                }
                return result;
            }
            set
            {
                _quiet++;
                SuspendLayout();
                try
                {
                    ClearRows();
                    if (value != null)
                    {
                        for (int i = 0; i < value.Count; i++)
                        {
                            if (value[i] != null)
                                AddRow(value[i].itemId, value[i].count, false);
                        }
                    }
                    UpdateEmptyState();
                }
                finally
                {
                    ResumeLayout(true);
                    _quiet--;
                }
            }
        }

        /// <summary>Moves focus to one material row, or to the add button when there are none.</summary>
        public void FocusEntry(int index)
        {
            int rows = _rows.RowCount;
            if (rows == 0)
            {
                _addButton.Focus();
                return;
            }
            Control picker = _rows.GetControlFromPosition(0, Math.Max(0, Math.Min(index, rows - 1)));
            if (picker != null)
                picker.Focus();
        }

        private void ClearRows()
        {
            List<Control> old = new List<Control>();
            foreach (Control control in _rows.Controls)
                old.Add(control);
            _rows.Controls.Clear();
            _rows.RowStyles.Clear();
            _rows.RowCount = 0;
            foreach (Control control in old)
                control.Dispose();
        }

        private void AddRow(string itemId, int amount, bool byUser)
        {
            int row = _rows.RowCount;
            _rows.RowCount = row + 1;
            _rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            ItemPicker picker = new ItemPicker(_catalog)
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 2, 6, 2),
                AccessibleName = UiFactory.StripMnemonic(Capitalize(_noun)) + " " + (row + 1)
            };
            picker.ItemId = itemId;
            picker.ItemChanged += delegate { RaiseChanged(); };

            ThemedNumericUpDown count = new ThemedNumericUpDown
            {
                Minimum = 0,
                Maximum = 99999,
                Width = 64,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 2, 6, 2),
                BorderStyle = BorderStyle.FixedSingle,
                Font = ManagerTheme.UiFont(9.5f),
                AccessibleName = "Amount of " + _noun + " " + (row + 1)
            };
            count.Value = Math.Max(count.Minimum, Math.Min(count.Maximum, amount));
            count.ValueChanged += delegate { RaiseChanged(); };

            ActionButton remove = UiFactory.Button("×", ActionButtonKind.Quiet, null);
            remove.AccessibleName = "Remove " + _noun + " " + (row + 1);
            remove.Margin = new Padding(0, 1, 0, 1);
            remove.MinimumSize = new Size(30, 0);
            remove.Font = ManagerTheme.UiFont(12f);
            // Deferred: the row (and this button) is rebuilt by the removal.
            remove.Click += delegate { BeginInvoke((MethodInvoker)delegate { RemoveRow(picker); }); };

            _rows.Controls.Add(picker, 0, row);
            _rows.Controls.Add(count, 1, row);
            _rows.Controls.Add(remove, 2, row);
            ManagerTheme.Apply(picker);
            ManagerTheme.Apply(count);

            if (byUser)
            {
                UpdateEmptyState();
                picker.Focus();
                RaiseChanged();
            }
        }

        private void RemoveRow(ItemPicker picker)
        {
            TableLayoutPanelCellPosition position = _rows.GetPositionFromControl(picker);
            if (position.Row < 0)
                return;
            List<ContentPackIngredient> materials = Materials;
            materials.RemoveAt(position.Row);
            int focusRow = position.Row;
            Materials = materials;
            if (materials.Count > 0)
                FocusEntry(Math.Min(focusRow, materials.Count - 1));
            else
                _addButton.Focus();
            RaiseChanged();
        }

        private void UpdateEmptyState()
        {
            _emptyLabel.Visible = _rows.RowCount == 0;
            _emptyLabel.Text = "No " + _noun + "s yet.";
        }

        private void RaiseChanged()
        {
            if (_quiet > 0)
                return;
            EventHandler handler = ValueChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private static string Capitalize(string value)
        {
            return string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
