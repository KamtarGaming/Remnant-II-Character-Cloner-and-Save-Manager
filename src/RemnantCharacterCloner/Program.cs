using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using lib.remnant2.saves.Model;
using lib.remnant2.saves.Model.Parts;
using lib.remnant2.saves.Model.Properties;

namespace RemnantCharacterCloner;

internal static class Ui
{
    public static bool IsFrench => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fr", StringComparison.OrdinalIgnoreCase);
    public static string T(string english, string french) => IsFrench ? french : english;
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly TextBox folderBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Button browseButton = new() { Text = Ui.T("Browse...", "Parcourir..."), AutoSize = true };
    private readonly Button refreshButton = new() { Text = Ui.T("Analyze", "Analyser"), AutoSize = true };
    private readonly ComboBox sourceBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox targetBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Button cloneButton = new() { Text = Ui.T("Copy character", "Copier le personnage"), AutoSize = true, Enabled = false };
    private readonly Button copyWorldButton = new() { Text = Ui.T("Copy world", "Copier le monde"), AutoSize = true, Enabled = false };
    private readonly Button exportButton = new() { Text = Ui.T("Export character...", "Exporter le personnage..."), AutoSize = true, Enabled = false };
    private readonly Button importButton = new() { Text = Ui.T("Import archive...", "Importer une archive..."), AutoSize = true, Enabled = false };
    private readonly Button deleteButton = new() { Text = Ui.T("Delete selected character", "Supprimer le personnage sélectionné"), AutoSize = true, Enabled = false };
    private readonly Button restoreButton = new() { Text = Ui.T("Restore a backup...", "Restaurer une sauvegarde..."), AutoSize = true };
    private readonly Button helpButton = new() { Text = Ui.T("How it works", "Comment ça marche"), AutoSize = true };
    private readonly Button recoveryButton = new() { Text = Ui.T("Advanced recovery...", "Récupération avancée..."), AutoSize = true };
    private readonly Label stateLabel = new() { AutoSize = false, AutoEllipsis = true, Height = 20, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolTip toolTip = new();
    private const string SavePathHint = @"C:\Users\%USERNAME%\Saved Games\Remnant2\Steam\";
    private readonly DataGridView grid = new()
    {
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        Dock = DockStyle.Fill,
        RowHeadersVisible = false
    };
    private List<SlotInfo> slots = [];

    public MainForm()
    {
        Text = Ui.T("Remnant II - Character Cloner", "Remnant II - Copie de personnage");
        Icon = LoadApplicationIcon();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 500);
        Size = new Size(1160, 540);

        grid.Columns.Add("slot", "Slot");
        grid.Columns.Add("state", Ui.T("Status", "État"));
        grid.Columns.Add("id", "ID");
        grid.Columns.Add("unrealName", "Unreal ID");
        grid.Columns.Add("location", Ui.T("Location", "Emplacement"));
        grid.Columns.Add("playTime", Ui.T("Play time", "Temps de jeu"));
        grid.Columns.Add("updated", Ui.T("Last world save", "Dernière sauvegarde monde"));
        grid.Columns.Add("power", Ui.T("Power", "Puissance"));
        grid.Columns.Add("archetypes", Ui.T("Archetypes", "Archétypes"));
        grid.Columns["slot"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        grid.Columns["state"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        grid.Columns["id"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        grid.Columns["unrealName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        grid.Columns["location"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid.Columns["playTime"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        grid.Columns["updated"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        grid.Columns["power"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        grid.Columns["archetypes"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 3, RowCount = 8 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var folderLabel = new Label { Text = Ui.T("Save folder:", "Dossier de sauvegarde :"), AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 1, 8, 0) };
        folderBox.Margin = new Padding(0, 0, 0, 0);
        root.Controls.Add(folderLabel, 0, 0);
        root.Controls.Add(folderBox, 1, 0);
        var topButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(6, 0, 0, 0) };
        browseButton.Margin = new Padding(0);
        refreshButton.Margin = new Padding(6, 0, 0, 0);
        topButtons.Controls.Add(browseButton);
        topButtons.Controls.Add(refreshButton);
        root.Controls.Add(topButtons, 2, 0);

        var hintPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Anchor = AnchorStyles.Left, Margin = new Padding(0) };
        var hintPrefix = new Label { Text = Ui.T("Usual location:", "Emplacement habituel :"), AutoSize = true, ForeColor = Color.DimGray, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 5, 0) };
        var hintLink = new LinkLabel { Text = SavePathHint, AutoSize = true, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(4, 2, 4, 2), LinkColor = Color.MidnightBlue, ActiveLinkColor = Color.DarkGreen, VisitedLinkColor = Color.MidnightBlue, Anchor = AnchorStyles.Left, Margin = new Padding(0) };
        var hintSuffix = new Label { Text = Ui.T(" then the Steam subfolder.", " puis le sous-dossier Steam."), AutoSize = true, ForeColor = Color.DimGray, Anchor = AnchorStyles.Left, Margin = new Padding(5, 0, 0, 0) };
        var copyPathButton = new Button { Text = Ui.T("Copy path", "Copier le chemin"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 0, 0, 0) };
        void CopyHintPath()
        {
            Clipboard.SetText(SavePathHint);
            SetState(Ui.T("Usual path copied to the clipboard.", "Chemin habituel copié dans le presse-papiers."), Color.DarkGreen);
        }
        hintLink.Click += (_, _) => CopyHintPath();
        copyPathButton.Click += (_, _) => CopyHintPath();
        hintPanel.Controls.Add(hintPrefix);
        hintPanel.Controls.Add(hintLink);
        hintPanel.Controls.Add(hintSuffix);
        root.SetColumnSpan(hintPanel, 2);
        root.Controls.Add(hintPanel, 0, 1);
        root.Controls.Add(copyPathButton, 2, 1);

        root.SetColumnSpan(stateLabel, 3);
        root.Controls.Add(stateLabel, 0, 2);
        root.SetColumnSpan(grid, 3);
        root.Controls.Add(grid, 0, 3);

        var maintenanceButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        maintenanceButtons.Controls.Add(exportButton);
        maintenanceButtons.Controls.Add(importButton);
        maintenanceButtons.Controls.Add(deleteButton);
        maintenanceButtons.Controls.Add(recoveryButton);
        root.SetColumnSpan(maintenanceButtons, 3);
        root.Controls.Add(maintenanceButtons, 0, 4);
        root.Controls.Add(new Label { Text = Ui.T("Character to copy:", "Personnage à copier :"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        root.Controls.Add(sourceBox, 1, 5);
        root.SetColumnSpan(sourceBox, 2);
        root.Controls.Add(new Label { Text = Ui.T("Target slot:", "Nouveau slot :"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        root.Controls.Add(targetBox, 1, 6);
        root.SetColumnSpan(targetBox, 2);
        var bottomLeftButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) };
        cloneButton.Margin = new Padding(0);
        copyWorldButton.Margin = new Padding(6, 0, 0, 0);
        bottomLeftButtons.Controls.Add(cloneButton);
        bottomLeftButtons.Controls.Add(copyWorldButton);
        var bottomRightButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Anchor = AnchorStyles.Top | AnchorStyles.Right, Margin = new Padding(0) };
        restoreButton.Margin = new Padding(0);
        helpButton.Margin = new Padding(0, 0, 6, 0);
        bottomRightButtons.Controls.Add(helpButton);
        bottomRightButtons.Controls.Add(restoreButton);
        root.SetColumnSpan(bottomLeftButtons, 2);
        root.Controls.Add(bottomLeftButtons, 0, 7);
        root.Controls.Add(bottomRightButtons, 2, 7);
        Controls.Add(root);

        browseButton.Click += (_, _) => Browse();
        refreshButton.Click += (_, _) => RefreshSlots();
        sourceBox.SelectedIndexChanged += (_, _) => UpdateButton();
        targetBox.SelectedIndexChanged += (_, _) => UpdateButton();
        cloneButton.Click += (_, _) => CloneSelected();
        copyWorldButton.Click += (_, _) => CopyWorldSelected();
        exportButton.Click += (_, _) => ExportSelected();
        importButton.Click += (_, _) => ImportSelected();
        deleteButton.Click += (_, _) => DeleteSelected();
        restoreButton.Click += (_, _) => RestoreSelectedBackup();
        helpButton.Click += (_, _) => ShowHelp();
        recoveryButton.Click += (_, _) => RecoverSharedCharacter();
        grid.SelectionChanged += (_, _) => UpdateButton();
        Shown += (_, _) => TryAutoSelectFolder();
        SetState(Ui.T("Choose the folder that contains profile.sav.", "Choisis le dossier qui contient profile.sav."), Color.DimGray);
    }

    private static Icon LoadApplicationIcon()
    {
        using Stream? stream = typeof(MainForm).Assembly.GetManifestResourceStream("RemnantCharacterCloner.logo.ico");
        if (stream is null) return SystemIcons.Application;
        using Icon icon = new(stream);
        return (Icon)icon.Clone();
    }

    private void TryAutoSelectFolder()
    {
        try
        {
            string steamRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games", "Remnant2", "Steam");
            if (!Directory.Exists(steamRoot)) return;
            string[] candidates = Directory.GetDirectories(steamRoot, "*", SearchOption.TopDirectoryOnly);
            if (candidates.Length != 1 || !File.Exists(Path.Combine(candidates[0], "profile.sav"))) return;
            folderBox.Text = candidates[0];
            RefreshSlots();
        }
        catch
        {
        }
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog { Description = Ui.T("Choose the folder containing profile.sav", "Choisis le dossier contenant profile.sav"), UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        folderBox.Text = dialog.SelectedPath;
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        try
        {
            string folder = RequireFolder();
            slots = SaveOperations.ReadSlots(folder);
            grid.Rows.Clear();
            sourceBox.Items.Clear();
            targetBox.Items.Clear();
            foreach (SlotInfo slot in slots)
            {
                int rowIndex = grid.Rows.Add(slot.Index + 1, slot.StateText, slot.IdText, slot.UnrealNameText, slot.WorldLocationText, slot.WorldPlayTimeText, slot.WorldUpdatedText, slot.Power, slot.Archetypes);
                DataGridViewRow row = grid.Rows[rowIndex];
                row.Tag = slot;
                if (slot.NeedsRepair)
                {
                    row.DefaultCellStyle.ForeColor = Color.Firebrick;
                    row.DefaultCellStyle.SelectionForeColor = Color.Firebrick;
                    row.DefaultCellStyle.SelectionBackColor = Color.MistyRose;
                }
                if (slot.Occupied)
                {
                    if (!slot.NeedsRepair) sourceBox.Items.Add(slot);
                }
                else targetBox.Items.Add(slot);
            }
            if (sourceBox.Items.Count > 0) sourceBox.SelectedIndex = 0;
            if (targetBox.Items.Count > 0) targetBox.SelectedIndex = 0;
            InstanceState? state = SaveOperations.ReadState(folder);
            string profileHash = SaveOperations.Hash(Path.Combine(folder, "profile.sav"));
            int invalidCount = slots.Count(x => x.NeedsRepair);
            if (invalidCount > 0)
                SetState(Ui.T($"{invalidCount} slot{(invalidCount > 1 ? "s" : "")} with an invalid ID. Select the red row to recover that character without deleting the other.", $"{invalidCount} slot{(invalidCount > 1 ? "s" : "")} avec un ID invalide. Sélectionne une ligne rouge pour réparer ce personnage sans supprimer l'autre."), Color.Firebrick);
            else if (state?.ResultProfileHash == profileHash)
                SetState(Ui.T($"This save folder was already processed on {state.CreatedAt.LocalDateTime:g}. Last backup: {ShortBackupPath(state.BackupFolder)}", $"Cette instance a déjà été traitée le {state.CreatedAt.LocalDateTime:g}. Dernière sauvegarde : {ShortBackupPath(state.BackupFolder)}"), Color.DarkGoldenrod);
            else if (state is not null)
                SetState(Ui.T($"This save folder was processed on {state.CreatedAt.LocalDateTime:g}, then its profile changed.", $"Cette instance a déjà été traitée le {state.CreatedAt.LocalDateTime:g}, puis son profil a changé."), Color.DarkGoldenrod);
            else
                SetState(targetBox.Items.Count == 0 ? Ui.T("No empty slot: no write is possible.", "Aucun slot libre : aucune écriture possible.") : Ui.T("Ready. A full backup of the .sav files will be made before each change.", "Prêt. Une sauvegarde complète des fichiers .sav sera créée avant toute modification."), Color.DarkGreen);
            UpdateButton();
        }
        catch (Exception ex)
        {
            slots = [];
            grid.Rows.Clear();
            sourceBox.Items.Clear();
            targetBox.Items.Clear();
            UpdateButton();
            SetState(ex.Message, Color.Firebrick);
        }
    }

    private void CloneSelected()
    {
        if (sourceBox.SelectedItem is not SlotInfo source || targetBox.SelectedItem is not SlotInfo target) return;
        string folder;
        try
        {
            folder = RequireFolder();
            if (target.Occupied) throw new InvalidOperationException("Le slot cible est déjà occupé.");
            string message = Ui.T($"Copy the character from slot {source.Index + 1} to slot {target.Index + 1}?\n\nThis action does not modify any save_N.sav world. Use Copy world separately if you also want to duplicate the world. A full backup will be created before any change.", $"Copier le personnage du slot {source.Index + 1} vers le slot {target.Index + 1} ?\n\nCette action ne modifie aucun monde save_N.sav. Utilise « Copier le monde » séparément si tu souhaites aussi dupliquer le monde. Une sauvegarde complète sera créée avant toute modification.");
            DialogResult answer = MessageBox.Show(this, message, Ui.T("Confirm", "Confirmer"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
            string backup = SaveOperations.Clone(folder, source.Index, target.Index, false, false);
            SetState(Ui.T($"Copy complete. Backup: {backup}", $"Copie terminée. Sauvegarde : {backup}"), Color.DarkGreen);
            RefreshSlots();
        }
        catch (Exception ex)
        {
            SetState(ex.Message, Color.Firebrick);
            MessageBox.Show(this, ex.Message, Ui.T("Copy cancelled", "Copie annulée"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyWorldSelected()
    {
        if (sourceBox.SelectedItem is not SlotInfo source || targetBox.SelectedItem is not SlotInfo target) return;
        try
        {
            string folder = RequireFolder();
            string sourceWorld = Path.Combine(folder, $"save_{source.Index}.sav");
            string targetWorld = Path.Combine(folder, $"save_{target.Index}.sav");
            if (!File.Exists(sourceWorld)) throw new InvalidOperationException($"{Path.GetFileName(sourceWorld)} est introuvable.");
            bool targetExists = File.Exists(targetWorld);
            string message = targetExists
                ? Ui.T($"Copy the world from slot {source.Index + 1} to slot {target.Index + 1}?\n\nThe existing target world will be replaced. The character will not be changed. A full backup will be created before any change.", $"Copier le monde du slot {source.Index + 1} vers le slot {target.Index + 1} ?\n\nLe monde cible existant sera remplacé. Le personnage ne sera pas modifié. Une sauvegarde complète sera créée avant toute modification.")
                : Ui.T($"Copy the world from slot {source.Index + 1} to slot {target.Index + 1}?\n\nThe character will not be changed. A full backup will be created before any change.", $"Copier le monde du slot {source.Index + 1} vers le slot {target.Index + 1} ?\n\nLe personnage ne sera pas modifié. Une sauvegarde complète sera créée avant toute modification.");
            if (MessageBox.Show(this, message, Ui.T("Copy world", "Copier le monde"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            string backup = SaveOperations.CopyWorld(folder, source.Index, target.Index, targetExists);
            SetState(Ui.T($"World copied. Backup: {ShortBackupPath(backup)}", $"Monde copié. Sauvegarde : {ShortBackupPath(backup)}"), Color.DarkGreen);
            RefreshSlots();
        }
        catch (Exception ex)
        {
            SetState(ex.Message, Color.Firebrick);
            MessageBox.Show(this, ex.Message, Ui.T("World copy cancelled", "Copie du monde annulée"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteSelected()
    {
        if (grid.SelectedRows.Count != 1 || grid.SelectedRows[0].Tag is not SlotInfo slot || !slot.Occupied) return;
        try
        {
            string folder = RequireFolder();
            string message = $"Supprimer définitivement le slot {slot.Index + 1} ?\n\nLe personnage et son monde save_{slot.Index}.sav seront retirés du dossier sélectionné. Une sauvegarde complète sera créée avant toute modification.";
            if (MessageBox.Show(this, message, "Supprimer le personnage", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            string backup = SaveOperations.DeleteSlot(folder, slot.Index);
            SetState($"Slot {slot.Index + 1} supprimé. Sauvegarde : {backup}", Color.DarkGreen);
            RefreshSlots();
        }
        catch (Exception ex)
        {
            SetState(ex.Message, Color.Firebrick);
            MessageBox.Show(this, ex.Message, "Suppression annulée", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportSelected()
    {
        if (grid.SelectedRows.Count != 1 || grid.SelectedRows[0].Tag is not SlotInfo slot || !slot.Occupied) return;
        try
        {
            string folder = RequireFolder();
            using var dialog = new SaveFileDialog { Filter = "Archive personnage Remnant II|*.r2character", FileName = $"RemnantII-slot-{slot.Index + 1}.r2character", AddExtension = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            SaveOperations.ExportCharacter(folder, slot.Index, dialog.FileName);
            SetState($"Personnage exporté : {Path.GetFileName(dialog.FileName)}", Color.DarkGreen);
        }
        catch (Exception ex)
        {
            SetState(ex.Message, Color.Firebrick);
            MessageBox.Show(this, ex.Message, "Export annulé", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportSelected()
    {
        if (targetBox.SelectedItem is not SlotInfo target) return;
        try
        {
            string folder = RequireFolder();
            using var dialog = new OpenFileDialog { Filter = "Archive personnage Remnant II|*.r2character", Multiselect = false };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            CharacterArchiveInfo info = SaveOperations.ReadCharacterArchive(dialog.FileName);
            bool copyWorld = false;
            if (info.HasWorld)
            {
                string targetWorld = Path.Combine(folder, $"save_{target.Index}.sav");
                string prompt = File.Exists(targetWorld)
                    ? $"Importer le personnage dans le slot {target.Index + 1}.\n\nOui : importer aussi son monde et remplacer le monde cible.\nNon : conserver le monde cible.\nAnnuler : ne rien importer."
                    : $"Importer le personnage dans le slot {target.Index + 1}.\n\nOui : importer aussi son monde.\nNon : importer seulement le personnage.\nAnnuler : ne rien importer.";
                DialogResult choice = MessageBox.Show(this, prompt, "Importer une archive", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button3);
                if (choice == DialogResult.Cancel) return;
                copyWorld = choice == DialogResult.Yes;
            }
            else if (MessageBox.Show(this, $"Importer le personnage dans le slot {target.Index + 1} ?\n\nL'archive ne contient pas de monde.", "Importer une archive", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            string backup = SaveOperations.ImportCharacter(folder, dialog.FileName, target.Index, copyWorld);
            SetState($"Archive importée. Sauvegarde : {ShortBackupPath(backup)}", Color.DarkGreen);
            RefreshSlots();
        }
        catch (Exception ex)
        {
            SetState(ex.Message, Color.Firebrick);
            MessageBox.Show(this, ex.Message, "Import annulé", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreSelectedBackup()
    {
        try
        {
            string folder = RequireFolder();
            List<BackupInfo> backups = SaveOperations.ListBackups(folder);
            if (backups.Count == 0) throw new InvalidOperationException("Aucune sauvegarde disponible dans CharacterClonerBackups.");
            using var dialog = new BackupPickerForm(backups);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedBackup is null) return;
            BackupInfo selected = dialog.SelectedBackup;
            string message = $"Restaurer la sauvegarde du {selected.CreatedAt.LocalDateTime:g} ?\n\nLes fichiers .sav actuels seront remplacés par ceux de cette sauvegarde. L'état actuel sera sauvegardé juste avant la restauration.";
            if (MessageBox.Show(this, message, "Restaurer une sauvegarde", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            string safetyBackup = SaveOperations.RestoreSelectedBackup(folder, selected.Folder);
            SetState($"Sauvegarde restaurée. État précédent : {safetyBackup}", Color.DarkGreen);
            RefreshSlots();
        }
        catch (Exception ex)
        {
            SetState(ex.Message, Color.Firebrick);
            MessageBox.Show(this, ex.Message, "Restauration annulée", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RecoverSharedCharacter()
    {
        try
        {
            string folder = RequireFolder();
            if (slots.Count == 0) slots = SaveOperations.ReadSlots(folder);
            using var dialog = new RecoveryPickerForm(slots);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Source is null || dialog.Target is null) return;
            string message = $"Rendre le slot {dialog.Target.Index + 1} indépendant à partir du personnage du slot {dialog.Source.Index + 1} ?\n\nLes deux mondes existants seront préservés : aucun fichier save_N.sav ne sera copié, remplacé ou supprimé. Cette opération est réservée aux deux slots qui pointent actuellement vers le même personnage. Une sauvegarde complète sera créée avant toute modification.";
            if (MessageBox.Show(this, message, "Récupération avancée", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            string backup = SaveOperations.RecoverSharedSlot(folder, dialog.Source.Index, dialog.Target.Index);
            SetState($"Personnages séparés sans toucher aux mondes. Sauvegarde : {ShortBackupPath(backup)}", Color.DarkGreen);
            RefreshSlots();
        }
        catch (Exception ex)
        {
            SetState(ex.Message, Color.Firebrick);
            MessageBox.Show(this, ex.Message, "Récupération annulée", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string RequireFolder()
    {
        if (string.IsNullOrWhiteSpace(folderBox.Text)) throw new InvalidOperationException("Choisis un dossier de sauvegarde.");
        string folder = Path.GetFullPath(folderBox.Text);
        if (!File.Exists(Path.Combine(folder, "profile.sav"))) throw new InvalidOperationException("profile.sav est introuvable dans ce dossier.");
        return folder;
    }

    private void UpdateButton()
    {
        cloneButton.Enabled = sourceBox.SelectedItem is SlotInfo && targetBox.SelectedItem is SlotInfo;
        copyWorldButton.Enabled = sourceBox.SelectedItem is SlotInfo && targetBox.SelectedItem is SlotInfo;
        deleteButton.Enabled = grid.SelectedRows.Count == 1 && grid.SelectedRows[0].Tag is SlotInfo occupiedSlot && occupiedSlot.Occupied;
        exportButton.Enabled = grid.SelectedRows.Count == 1 && grid.SelectedRows[0].Tag is SlotInfo exportSlot && exportSlot.Occupied;
        importButton.Enabled = targetBox.SelectedItem is SlotInfo;
    }

    private void SetState(string text, Color color)
    {
        stateLabel.Text = text;
        stateLabel.ForeColor = color;
        toolTip.SetToolTip(stateLabel, text);
    }

    private static string ShortBackupPath(string path)
    {
        string[] pieces = Path.TrimEndingDirectorySeparator(path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return pieces.Length <= 2 ? path : $"...{Path.DirectorySeparatorChar}{string.Join(Path.DirectorySeparatorChar, pieces[^2..])}";
    }

    private void ShowHelp()
    {
        using var dialog = new HelpForm();
        dialog.ShowDialog(this);
    }
}

internal sealed record SlotInfo(int Index, bool Occupied, int Power, string Archetypes, int? CharacterId, string UnrealName, DateTimeOffset? WorldUpdatedAt, TimeSpan? WorldPlayTime, string WorldLocation, string IdIssue)
{
    public bool NeedsRepair => Occupied && IdIssue.Length > 0;
    public string StateText => NeedsRepair ? Ui.T("Invalid ID", "ID invalide") : Occupied ? Ui.T("Used", "Occupé") : Ui.T("Empty", "Libre");
    public string IdText => Occupied ? CharacterId?.ToString() ?? (Index == 0 ? Ui.T("implicit 0", "0 implicite") : Ui.T("Missing", "Absent")) : "—";
    public string UnrealNameText => Occupied ? UnrealName : "—";
    public string WorldLocationText => Occupied ? WorldLocation : "—";
    public string WorldPlayTimeText => Occupied && WorldPlayTime is TimeSpan time ? $"{(int)time.TotalHours} h {time.Minutes:D2} min" : "—";
    public string WorldUpdatedText => Occupied ? WorldUpdatedAt?.LocalDateTime.ToString("g") ?? "Absent" : "—";
    public override string ToString() => Occupied ? Ui.T($"Slot {Index + 1} — power {Power} — {Archetypes}", $"Slot {Index + 1} — puissance {Power} — {Archetypes}") : Ui.T($"Slot {Index + 1} — empty", $"Slot {Index + 1} — libre");
}

internal sealed record WorldInfo(DateTimeOffset? UpdatedAt, TimeSpan? PlayTime, string Location);

internal sealed record CharacterArchiveInfo(int SourceSlot, bool HasWorld, string Description);

internal sealed class CharacterArchiveManifest
{
    public string Format { get; set; } = "RemnantCharacterCloner.CharacterArchive";
    public int Version { get; set; } = 1;
    public int SourceSlot { get; set; }
    public bool HasWorld { get; set; }
    public string Description { get; set; } = "";
}

internal sealed class InstanceState
{
    public DateTimeOffset CreatedAt { get; set; }
    public int SourceSlot { get; set; }
    public int TargetSlot { get; set; }
    public string BackupFolder { get; set; } = "";
    public string ResultProfileHash { get; set; } = "";
}

internal sealed record BackupInfo(string Folder, DateTimeOffset CreatedAt, int FileCount)
{
    public override string ToString() => $"{CreatedAt.LocalDateTime:dd/MM/yyyy HH:mm:ss} — {FileCount} fichier{(FileCount > 1 ? "s" : "")}";
}

internal sealed class BackupPickerForm : Form
{
    private readonly ListBox list = new() { Dock = DockStyle.Fill };
    public BackupInfo? SelectedBackup => list.SelectedItem as BackupInfo;

    public BackupPickerForm(List<BackupInfo> backups)
    {
        Text = Ui.T("Restore a backup", "Restaurer une sauvegarde");
        Icon = LoadIcon();
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(460, 300);
        Size = new Size(560, 390);
        list.Items.AddRange(backups.Cast<object>().ToArray());
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        var description = new Label { Text = Ui.T("Choose a backup. The current state will be backed up before restoration.", "Choisis une sauvegarde. L'état actuel sera sauvegardé avant restauration."), AutoSize = true, Padding = new Padding(12, 12, 12, 6) };
        var restore = new Button { Text = Ui.T("Restore", "Restaurer"), DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = Ui.T("Cancel", "Annuler"), DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(restore);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(description, 0, 0);
        layout.Controls.Add(list, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        AcceptButton = restore;
        CancelButton = cancel;
    }

    private static Icon LoadIcon()
    {
        using Stream? stream = typeof(MainForm).Assembly.GetManifestResourceStream("RemnantCharacterCloner.logo.ico");
        if (stream is null) return SystemIcons.Application;
        using Icon icon = new(stream);
        return (Icon)icon.Clone();
    }
}

internal sealed class HelpForm : Form
{
    public HelpForm()
    {
        Text = Ui.T("How it works", "Comment ça marche");
        Icon = LoadIcon();
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(680, 520);
        Size = new Size(760, 650);
        var text = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            Font = new Font(FontFamily.GenericSansSerif, 9.5f),
            Text = Ui.T(
                "HOW THE FORMAT IS READ\r\n\r\n"
                + "This program is written in C#. It uses a library able to decompress Remnant II saves, read their object table and write them again with a valid checksum. The original file is only replaced after it is read again and passes internal checks.\r\n\r\n"
                + "1. PROFILE.SAV: THE LIST AND CHARACTER DATA\r\n"
                + "profile.sav contains a table of numbered objects. Its root object owns a Characters array. Each position in that array is a slot and holds an internal ObjectIndex reference. That reference identifies the object that actually contains character statistics, equipment, archetypes and progression.\r\n\r\n"
                + "An empty slot has an empty reference (-1). The first slot may have no ID property: it is then interpreted as internal ID 0. Other slots normally have an ID matching their internal position.\r\n\r\n"
                + "2. SAVE_N.SAV: THE WORLD\r\n"
                + "The world is not stored in profile.sav. save_0.sav belongs to the first displayed slot, save_1.sav to the second, and so on. It holds world state, progression and areas. That is why character recovery can preserve both world files unchanged.\r\n\r\n"
                + "3. HOW A CHARACTER IS ISOLATED\r\n"
                + "The program starts from the selected character ObjectIndex. It follows every reachable ObjectProperty reference from that object: this is the character data graph. Each object in that graph is cloned with a new unused index. Internal references are then remapped to point to the new objects. Finally, the target slot reference is replaced by the new root and its ID is set to the slot index.\r\n\r\n"
                + "4. DUPLICATE DETECTION\r\n"
                + "Two slots showing the same ObjectIndex literally point to the same character. Leveling up or moving one then changes the other because there is only one character root. Recovery creates a second SavedCharacter root, assigns it a distinct Unreal internal name and sets both slot IDs; already shared account references are retained. The save_0.sav and save_1.sav worlds are not included in that rewrite.\r\n\r\n"
                + "5. CHECKS AFTER WRITING\r\n"
                + "The new profile.sav is read again. The program checks that both roots and their internal names differ, that the new root does not reference the source character root, and that slot IDs are correct. It also calculates the SHA-256 hash of every save_N.sav before and after: a different hash cancels the operation and restores the backup.\r\n\r\n"
                + "6. LIMITS\r\n"
                + "The format is binary and not officially documented. The checks prove structural consistency, but only launching the game confirms full compatibility with a given Remnant II version. A full .sav backup is therefore made before every write.",
                "COMMENT LE FORMAT EST LU\r\n\r\n"
                + "Ce programme est écrit en C#. Il s'appuie sur une bibliothèque capable de décompresser les sauvegardes Remnant II, de lire leur table d'objets puis de les réécrire avec un checksum valide. Le fichier d'origine n'est remplacé qu'après la relecture et les contrôles internes.\r\n\r\n"
                + "1. PROFILE.SAV : LA LISTE ET LES DONNÉES\r\n"
                + "profile.sav contient une table d'objets numérotés. Son objet racine possède un tableau « Characters ». Chaque position de ce tableau est un slot et contient une référence interne appelée ObjectIndex. Cette référence désigne l'objet qui porte réellement les statistiques, l'équipement, les archétypes et la progression du personnage.\r\n\r\n"
                + "Un slot libre contient une référence vide (-1). Un premier slot peut ne pas contenir la propriété ID : il est alors interprété comme l'ID interne 0. Les autres slots doivent normalement avoir un ID égal à leur position interne.\r\n\r\n"
                + "2. SAVE_N.SAV : LE MONDE\r\n"
                + "Le monde n'est pas rangé dans profile.sav. save_0.sav est associé au premier slot affiché, save_1.sav au deuxième, etc. Il contient notamment l'état du monde, sa progression et ses zones. C'est pourquoi une réparation de personnage peut conserver intégralement les deux fichiers monde.\r\n\r\n"
                + "3. COMMENT UN PERSONNAGE EST ISOLÉ\r\n"
                + "Le programme part de l'ObjectIndex du personnage choisi. Il parcourt toutes les références ObjectProperty atteignables depuis cet objet : c'est le graphe de données du personnage. Chaque objet de ce graphe est cloné avec un nouvel index inutilisé. Les références internes sont ensuite remappées pour qu'elles pointent vers les nouveaux objets. Enfin, la référence du slot cible est remplacée par la nouvelle racine et son ID est fixé à l'index du slot.\r\n\r\n"
                + "4. DÉTECTION D'UN DOUBLON\r\n"
                + "Deux slots qui affichent le même ObjectIndex pointent littéralement vers le même personnage. Monter de niveau ou changer de position sur l'un modifie alors l'autre, car il n'existe qu'une seule racine de personnage. La récupération crée une seconde racine SavedCharacter, lui attribue un nom interne Unreal distinct et fixe les IDs des deux slots ; les références de compte déjà partagées sont conservées. Les mondes save_0.sav et save_1.sav ne sont pas inclus dans cette réécriture.\r\n\r\n"
                + "5. CONTRÔLES APRÈS ÉCRITURE\r\n"
                + "Le nouveau profile.sav est relu. Le programme vérifie que les deux racines et leurs noms internes sont différents, que la nouvelle racine ne référence pas celle du personnage source et que les IDs des slots sont corrects. Il calcule aussi le hash SHA-256 de chaque save_N.sav avant et après : un hash différent annule l'opération et restaure le backup.\r\n\r\n"
                + "6. LIMITES\r\n"
                + "Le format est binaire et non documenté officiellement. Les vérifications prouvent la cohérence structurelle du fichier, mais seul le lancement du jeu confirme la compatibilité complète avec une version donnée de Remnant II. Une sauvegarde complète des fichiers .sav est donc prise avant chaque écriture.")
        };
        var close = new Button { Text = Ui.T("Close", "Fermer"), DialogResult = DialogResult.OK, AutoSize = true };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12) };
        buttons.Controls.Add(close);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(text, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);
        AcceptButton = close;
    }

    private static Icon LoadIcon()
    {
        using Stream? stream = typeof(MainForm).Assembly.GetManifestResourceStream("RemnantCharacterCloner.logo.ico");
        if (stream is null) return SystemIcons.Application;
        using Icon icon = new(stream);
        return (Icon)icon.Clone();
    }
}

internal sealed class RecoveryPickerForm : Form
{
    private readonly ComboBox sourceBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox targetBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly CheckBox copyCheck = new() { Text = Ui.T("I confirm the game is closed. Both worlds will be kept unchanged.", "Je confirme avoir fermé le jeu. Les deux mondes seront conservés sans modification."), AutoSize = true };
    private readonly Button recoverButton = new() { Text = Ui.T("Prepare recovery", "Préparer la récupération"), DialogResult = DialogResult.OK, AutoSize = true, Enabled = false };

    public SlotInfo? Source => sourceBox.SelectedItem as SlotInfo;
    public SlotInfo? Target => targetBox.SelectedItem as SlotInfo;

    public RecoveryPickerForm(IEnumerable<SlotInfo> allSlots)
    {
        Text = Ui.T("Advanced recovery", "Récupération avancée");
        Icon = LoadIcon();
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(620, 300);
        Size = new Size(720, 350);
        SlotInfo[] occupied = allSlots.Where(slot => slot.Occupied).ToArray();
        sourceBox.Items.AddRange(occupied.Cast<object>().ToArray());
        targetBox.Items.AddRange(occupied.Cast<object>().ToArray());
        if (sourceBox.Items.Count > 0) sourceBox.SelectedIndex = 0;
        if (targetBox.Items.Count > 1) targetBox.SelectedIndex = 1;
        var explanation = new Label
        {
            Text = Ui.T("Use this procedure only when two slots point to the same character. It creates a second character entry in profile.sav. The save_0.sav, save_1.sav and other world files are never written, copied or deleted. To replace a world afterwards, use Copy world separately.", "Utilise cette procédure uniquement lorsque deux slots pointent vers le même personnage. Elle crée une seconde entrée de personnage dans profile.sav. Les fichiers save_0.sav, save_1.sav et les autres mondes ne sont jamais écrits, copiés ou supprimés. Si tu veux ensuite remplacer un monde, utilise séparément le bouton Copier le monde."),
            AutoSize = true,
            MaximumSize = new Size(660, 0),
            Padding = new Padding(0, 0, 0, 8)
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.SetColumnSpan(explanation, 2);
        layout.Controls.Add(explanation, 0, 0);
        layout.Controls.Add(new Label { Text = Ui.T("Character to duplicate:", "Personnage à dupliquer :"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(sourceBox, 1, 1);
        layout.Controls.Add(new Label { Text = Ui.T("Slot to rebuild:", "Slot à reconstruire :"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(targetBox, 1, 2);
        layout.SetColumnSpan(copyCheck, 2);
        layout.Controls.Add(copyCheck, 0, 3);
        var cancel = new Button { Text = Ui.T("Cancel", "Annuler"), DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(recoverButton);
        layout.SetColumnSpan(buttons, 2);
        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);
        copyCheck.CheckedChanged += (_, _) => UpdateButton();
        sourceBox.SelectedIndexChanged += (_, _) => UpdateButton();
        targetBox.SelectedIndexChanged += (_, _) => UpdateButton();
        AcceptButton = recoverButton;
        CancelButton = cancel;
    }

    private void UpdateButton()
    {
        recoverButton.Enabled = copyCheck.Checked && Source is not null && Target is not null && Source.Index != Target.Index;
    }

    private static Icon LoadIcon()
    {
        using Stream? stream = typeof(MainForm).Assembly.GetManifestResourceStream("RemnantCharacterCloner.logo.ico");
        if (stream is null) return SystemIcons.Application;
        using Icon icon = new(stream);
        return (Icon)icon.Clone();
    }
}

internal static class SaveOperations
{
    private const string StateFileName = ".remnant-character-cloner.json";

    public static List<SlotInfo> ReadSlots(string folder)
    {
        string profilePath = Path.Combine(folder, "profile.sav");
        SaveFile save = SaveFile.Read(profilePath);
        List<ObjectProperty> characters = Characters(save).Items.Select(x => (ObjectProperty)x!).ToList();
        List<(bool Occupied, int Power, string Archetypes, int? CharacterId, string UnrealName, DateTimeOffset? WorldUpdatedAt, TimeSpan? WorldPlayTime, string WorldLocation)> raw = [];
        for (int index = 0; index < characters.Count; index++)
        {
            UObject? character = characters[index].Object;
            if (character is null)
            {
                raw.Add((false, 0, "", null, "", null, null, ""));
                continue;
            }
            PropertyBag props = character.Properties ?? throw new InvalidOperationException($"Le slot {index + 1} ne contient pas de propriétés de personnage.");
            int power = Integer(props, "PowerLevel");
            string first = Asset(props, "Archetype");
            string second = Asset(props, "SecondaryArchetype");
            WorldInfo world = ReadWorldInfo(Path.Combine(folder, $"save_{index}.sav"));
            raw.Add((true, power, string.Join(" / ", new[] { first, second }.Where(x => x.Length > 0)), CharacterId(props), UnrealName(character), world.UpdatedAt, world.PlayTime, world.Location));
        }
        Dictionary<int, List<int>> idSlots = raw
            .Select((slot, index) => (slot, index))
            .Where(x => x.slot.Occupied && x.slot.CharacterId.HasValue)
            .GroupBy(x => x.slot.CharacterId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(x => x.index).ToList());
        List<SlotInfo> result = [];
        for (int index = 0; index < raw.Count; index++)
        {
            var slot = raw[index];
            string issue = "";
            if (slot.Occupied)
            {
                if (!slot.CharacterId.HasValue && index != 0) issue = $"ID absent au lieu de {index}";
                else if (slot.CharacterId is int characterId && characterId != index)
                {
                    issue = $"ID {characterId} au lieu de {index}";
                    if (idSlots[characterId].Count > 1)
                    {
                        string related = string.Join(", ", idSlots[characterId].Where(x => x != index).Select(x => (x + 1).ToString()));
                        issue += $" ; partagé avec le slot {related}";
                    }
                }
            }
            result.Add(new SlotInfo(index, slot.Occupied, slot.Power, slot.Archetypes, slot.CharacterId, slot.UnrealName, slot.WorldUpdatedAt, slot.WorldPlayTime, slot.WorldLocation, issue));
        }
        return result;
    }

    public static string Clone(string folder, int sourceSlot, int targetSlot, bool copySourceWorld, bool replaceTargetWorld)
    {
        string profilePath = Path.Combine(folder, "profile.sav");
        string sourceWorld = Path.Combine(folder, $"save_{sourceSlot}.sav");
        string targetWorld = Path.Combine(folder, $"save_{targetSlot}.sav");
        if (copySourceWorld && !File.Exists(sourceWorld)) throw new InvalidOperationException($"{Path.GetFileName(sourceWorld)} est introuvable.");
        if (copySourceWorld && File.Exists(targetWorld) && !replaceTargetWorld) throw new InvalidOperationException($"{Path.GetFileName(targetWorld)} existe déjà.");
        string backup = CreateBackup(folder);
        string profileTemp = profilePath + ".character-cloner.tmp";
        string worldTemp = targetWorld + ".character-cloner.tmp";
        try
        {
            SaveFile save = SaveFile.Read(profilePath);
            ArrayProperty characterArray = Characters(save);
            List<ObjectProperty> slots = characterArray.Items.Select(x => (ObjectProperty)x!).ToList();
            if (sourceSlot < 0 || sourceSlot >= slots.Count || slots[sourceSlot].Object is null) throw new InvalidOperationException("Le personnage source est introuvable.");
            if (targetSlot < 0 || targetSlot >= slots.Count || slots[targetSlot].Object is not null) throw new InvalidOperationException("Le slot cible n'est plus libre.");
            int sourceRoot = slots[sourceSlot].ObjectIndex;
            SetCharacterSlotId(save.SaveData.Objects[sourceRoot], save.SaveData, sourceSlot);
            HashSet<int> graph = CollectObjectGraph(save.SaveData, sourceRoot);
            Dictionary<int, int> remap = [];
            foreach (int oldIndex in graph.OrderBy(x => x)) remap.Add(oldIndex, save.SaveData.Objects.Count + remap.Count);
            foreach (int oldIndex in graph.OrderBy(x => x))
            {
                UObject copy = (UObject)DeepClone(save.SaveData.Objects[oldIndex], new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;
                copy.ObjectIndex = remap[oldIndex];
                RemapObjectReferences(copy, remap);
                save.SaveData.Objects.Add(copy);
            }
            ObjectProperty newCharacter = (ObjectProperty)DeepClone(slots[sourceSlot], new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;
            newCharacter.ObjectIndex = remap[sourceRoot];
            newCharacter.ClassName = save.SaveData.Objects[newCharacter.ObjectIndex].ObjectPath;
            newCharacter.Object = null;
            EnsureUniqueObjectName(save.SaveData.Objects[newCharacter.ObjectIndex], save.SaveData);
            SetCharacterSlotId(save.SaveData.Objects[newCharacter.ObjectIndex], save.SaveData, targetSlot);
            characterArray.Items[targetSlot] = newCharacter;
            SaveFile.Write(profileTemp, save);
            ValidateProfile(profileTemp, sourceSlot, targetSlot);
            if (copySourceWorld)
            {
                File.Copy(sourceWorld, worldTemp, true);
                if (!Hash(sourceWorld).Equals(Hash(worldTemp), StringComparison.Ordinal)) throw new InvalidOperationException("La copie du monde n'a pas pu être vérifiée.");
            }
            File.Move(profileTemp, profilePath, true);
            if (copySourceWorld) File.Move(worldTemp, targetWorld, replaceTargetWorld);
            WriteState(folder, new InstanceState
            {
                CreatedAt = DateTimeOffset.Now,
                SourceSlot = sourceSlot,
                TargetSlot = targetSlot,
                BackupFolder = backup,
                ResultProfileHash = Hash(profilePath)
            });
            return backup;
        }
        catch
        {
            DeleteIfExists(profileTemp);
            DeleteIfExists(worldTemp);
            if (copySourceWorld) DeleteIfExists(targetWorld);
            RestoreBackup(backup, folder);
            throw;
        }
    }

    public static string CopyWorld(string folder, int sourceSlot, int targetSlot, bool replaceTargetWorld)
    {
        if (sourceSlot == targetSlot) throw new InvalidOperationException("Le monde source et le monde cible doivent être différents.");
        string sourceWorld = Path.Combine(folder, $"save_{sourceSlot}.sav");
        string targetWorld = Path.Combine(folder, $"save_{targetSlot}.sav");
        if (!File.Exists(sourceWorld)) throw new InvalidOperationException($"{Path.GetFileName(sourceWorld)} est introuvable.");
        if (File.Exists(targetWorld) && !replaceTargetWorld) throw new InvalidOperationException($"{Path.GetFileName(targetWorld)} existe déjà.");
        string backup = CreateBackup(folder);
        string worldTemp = targetWorld + ".character-cloner.tmp";
        try
        {
            File.Copy(sourceWorld, worldTemp, true);
            if (!Hash(sourceWorld).Equals(Hash(worldTemp), StringComparison.Ordinal)) throw new InvalidOperationException("La copie du monde n'a pas pu être vérifiée.");
            File.Move(worldTemp, targetWorld, replaceTargetWorld);
            WriteState(folder, new InstanceState
            {
                CreatedAt = DateTimeOffset.Now,
                SourceSlot = sourceSlot,
                TargetSlot = targetSlot,
                BackupFolder = backup,
                ResultProfileHash = Hash(Path.Combine(folder, "profile.sav"))
            });
            return backup;
        }
        catch
        {
            DeleteIfExists(worldTemp);
            RestoreBackup(backup, folder);
            throw;
        }
    }

    public static void ExportCharacter(string folder, int sourceSlot, string archivePath)
    {
        SaveFile save = SaveFile.Read(Path.Combine(folder, "profile.sav"));
        List<ObjectProperty> slots = Characters(save).Items.Select(x => (ObjectProperty)x!).ToList();
        if (sourceSlot < 0 || sourceSlot >= slots.Count || slots[sourceSlot].Object is null) throw new InvalidOperationException("Le personnage à exporter est introuvable.");
        UObject character = slots[sourceSlot].Object!;
        string fullArchivePath = Path.GetFullPath(archivePath);
        string temp = Path.Combine(Path.GetTempPath(), "RemnantCharacterArchive-" + Guid.NewGuid().ToString("N"));
        string archiveTemp = fullArchivePath + ".tmp";
        try
        {
            Directory.CreateDirectory(temp);
            File.Copy(Path.Combine(folder, "profile.sav"), Path.Combine(temp, "profile.sav"));
            string world = Path.Combine(folder, $"save_{sourceSlot}.sav");
            bool hasWorld = File.Exists(world);
            if (hasWorld) File.Copy(world, Path.Combine(temp, "world.sav"));
            PropertyBag properties = character.Properties ?? throw new InvalidOperationException("Le personnage ne contient pas de propriétés.");
            File.WriteAllText(Path.Combine(temp, "manifest.json"), JsonSerializer.Serialize(new CharacterArchiveManifest
            {
                SourceSlot = sourceSlot,
                HasWorld = hasWorld,
                Description = $"Slot {sourceSlot + 1} — puissance {Integer(properties, "PowerLevel")}"
            }, new JsonSerializerOptions { WriteIndented = true }));
            DeleteIfExists(archiveTemp);
            ZipFile.CreateFromDirectory(temp, archiveTemp, CompressionLevel.Optimal, false);
            File.Move(archiveTemp, fullArchivePath, true);
        }
        finally
        {
            DeleteIfExists(archiveTemp);
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    public static CharacterArchiveInfo ReadCharacterArchive(string archivePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        CharacterArchiveManifest manifest = ReadArchiveManifest(archive);
        if (archive.GetEntry("profile.sav") is null) throw new InvalidOperationException("L'archive ne contient pas profile.sav.");
        if (manifest.HasWorld && archive.GetEntry("world.sav") is null) throw new InvalidOperationException("L'archive annonce un monde absent.");
        return new CharacterArchiveInfo(manifest.SourceSlot, manifest.HasWorld, manifest.Description);
    }

    public static string ImportCharacter(string folder, string archivePath, int targetSlot, bool copyArchiveWorld)
    {
        string temp = Path.Combine(Path.GetTempPath(), "RemnantCharacterArchive-" + Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(folder, "profile.sav");
        string profileTemp = profilePath + ".character-cloner.tmp";
        string targetWorld = Path.Combine(folder, $"save_{targetSlot}.sav");
        string worldTemp = targetWorld + ".character-cloner.tmp";
        string backup = CreateBackup(folder);
        try
        {
            Directory.CreateDirectory(temp);
            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                CharacterArchiveManifest manifest = ReadArchiveManifest(archive);
                ZipArchiveEntry profileEntry = archive.GetEntry("profile.sav") ?? throw new InvalidOperationException("L'archive ne contient pas profile.sav.");
                ExtractArchiveEntry(profileEntry, Path.Combine(temp, "profile.sav"));
                if (copyArchiveWorld)
                {
                    if (!manifest.HasWorld) throw new InvalidOperationException("L'archive ne contient pas de monde.");
                    ExtractArchiveEntry(archive.GetEntry("world.sav") ?? throw new InvalidOperationException("Le monde de l'archive est introuvable."), Path.Combine(temp, "world.sav"));
                }
                SaveFile source = SaveFile.Read(Path.Combine(temp, "profile.sav"));
                SaveFile destination = SaveFile.Read(profilePath);
                ArrayProperty destinationCharacters = Characters(destination);
                List<ObjectProperty> targetSlots = destinationCharacters.Items.Select(x => (ObjectProperty)x!).ToList();
                if (targetSlot < 0 || targetSlot >= targetSlots.Count || targetSlots[targetSlot].Object is not null) throw new InvalidOperationException("Le slot cible n'est plus libre.");
                ArrayProperty sourceCharacters = Characters(source);
                if (manifest.SourceSlot < 0 || manifest.SourceSlot >= sourceCharacters.Items.Count || (sourceCharacters.Items[manifest.SourceSlot] as ObjectProperty)?.Object is null) throw new InvalidOperationException("Le personnage source de l'archive est introuvable.");
                ObjectProperty sourceCharacter = (ObjectProperty)sourceCharacters.Items[manifest.SourceSlot]!;
                HashSet<int> graph = CollectObjectGraph(source.SaveData, sourceCharacter.ObjectIndex);
                Dictionary<int, int> remap = [];
                foreach (int oldIndex in graph.OrderBy(x => x)) remap.Add(oldIndex, destination.SaveData.Objects.Count + remap.Count);
                foreach (int oldIndex in graph.OrderBy(x => x))
                {
                    UObject copy = (UObject)DeepClone(source.SaveData.Objects[oldIndex], new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;
                    copy.ObjectIndex = remap[oldIndex];
                    RemapObjectReferences(copy, remap);
                    destination.SaveData.Objects.Add(copy);
                }
                ObjectProperty imported = (ObjectProperty)DeepClone(sourceCharacter, new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;
                imported.ObjectIndex = remap[sourceCharacter.ObjectIndex];
                imported.ClassName = destination.SaveData.Objects[imported.ObjectIndex].ObjectPath;
                imported.Object = null;
                EnsureUniqueObjectName(destination.SaveData.Objects[imported.ObjectIndex], destination.SaveData);
                SetCharacterSlotId(destination.SaveData.Objects[imported.ObjectIndex], destination.SaveData, targetSlot);
                destinationCharacters.Items[targetSlot] = imported;
                SaveFile.Write(profileTemp, destination);
                ValidateImportedProfile(profileTemp, targetSlot);
                if (copyArchiveWorld)
                {
                    File.Copy(Path.Combine(temp, "world.sav"), worldTemp, true);
                    File.Move(worldTemp, targetWorld, true);
                }
                File.Move(profileTemp, profilePath, true);
                WriteState(folder, new InstanceState { CreatedAt = DateTimeOffset.Now, SourceSlot = manifest.SourceSlot, TargetSlot = targetSlot, BackupFolder = backup, ResultProfileHash = Hash(profilePath) });
                return backup;
            }
        }
        catch
        {
            DeleteIfExists(profileTemp);
            DeleteIfExists(worldTemp);
            RestoreBackup(backup, folder);
            throw;
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    public static string RecoverSharedSlot(string folder, int sourceSlot, int targetSlot)
    {
        string profilePath = Path.Combine(folder, "profile.sav");
        if (sourceSlot == targetSlot) throw new InvalidOperationException("Le personnage source et le slot à reconstruire doivent être différents.");
        Dictionary<string, string> worldsBefore = WorldHashes(folder);
        string backup = CreateBackup(folder);
        string profileTemp = profilePath + ".character-cloner.tmp";
        try
        {
            SaveFile save = SaveFile.Read(profilePath);
            ArrayProperty characterArray = Characters(save);
            List<ObjectProperty> slots = characterArray.Items.Select(x => (ObjectProperty)x!).ToList();
            if (sourceSlot < 0 || sourceSlot >= slots.Count || slots[sourceSlot].Object is null) throw new InvalidOperationException("Le personnage source est introuvable.");
            if (targetSlot < 0 || targetSlot >= slots.Count || slots[targetSlot].Object is null) throw new InvalidOperationException("Le slot à reconstruire est introuvable.");
            if (slots[sourceSlot].ObjectIndex != slots[targetSlot].ObjectIndex)
                throw new InvalidOperationException("Cet assistant est réservé à deux slots qui pointent vers la même donnée de personnage.");
            int sourceRoot = slots[sourceSlot].ObjectIndex;
            SetCharacterSlotId(save.SaveData.Objects[sourceRoot], save.SaveData, sourceSlot);
            UObject recoveredRoot = (UObject)DeepClone(save.SaveData.Objects[sourceRoot], new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;
            recoveredRoot.ObjectIndex = save.SaveData.Objects.Count;
            ClearCachedObjectReferences(recoveredRoot);
            save.SaveData.Objects.Add(recoveredRoot);
            EnsureUniqueObjectName(recoveredRoot, save.SaveData);
            ObjectProperty recovered = (ObjectProperty)DeepClone(slots[sourceSlot], new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;
            recovered.ObjectIndex = recoveredRoot.ObjectIndex;
            recovered.ClassName = recoveredRoot.ObjectPath;
            recovered.Object = null;
            SetCharacterSlotId(recoveredRoot, save.SaveData, targetSlot);
            characterArray.Items[targetSlot] = recovered;
            SaveFile.Write(profileTemp, save);
            ValidateSharedCharacterRecovery(profileTemp, sourceSlot, targetSlot, sourceRoot);
            File.Move(profileTemp, profilePath, true);
            ValidateWorldsUnchanged(folder, worldsBefore);
            WriteState(folder, new InstanceState
            {
                CreatedAt = DateTimeOffset.Now,
                SourceSlot = sourceSlot,
                TargetSlot = targetSlot,
                BackupFolder = backup,
                ResultProfileHash = Hash(profilePath)
            });
            return backup;
        }
        catch
        {
            DeleteIfExists(profileTemp);
            RestoreBackup(backup, folder);
            throw;
        }
    }

    public static string DeleteSlot(string folder, int slotIndex)
    {
        string profilePath = Path.Combine(folder, "profile.sav");
        string worldPath = Path.Combine(folder, $"save_{slotIndex}.sav");
        string backup = CreateBackup(folder);
        string profileTemp = profilePath + ".character-cloner.tmp";
        try
        {
            SaveFile save = SaveFile.Read(profilePath);
            ArrayProperty characters = Characters(save);
            if (slotIndex < 0 || slotIndex >= characters.Items.Count) throw new InvalidOperationException("Le slot à supprimer est introuvable.");
            if ((characters.Items[slotIndex] as ObjectProperty)?.Object is null) throw new InvalidOperationException("Le slot à supprimer est déjà libre.");
            characters.Items[slotIndex] = new ObjectProperty { ObjectIndex = -1 };
            SetFallbackActiveSlot(save, slotIndex);
            SaveFile.Write(profileTemp, save);
            ValidateDeletedSlot(profileTemp, slotIndex);
            File.Move(profileTemp, profilePath, true);
            DeleteIfExists(worldPath);
            WriteState(folder, new InstanceState
            {
                CreatedAt = DateTimeOffset.Now,
                SourceSlot = -1,
                TargetSlot = slotIndex,
                BackupFolder = backup,
                ResultProfileHash = Hash(profilePath)
            });
            return backup;
        }
        catch
        {
            DeleteIfExists(profileTemp);
            RestoreBackup(backup, folder);
            throw;
        }
    }

    public static List<BackupInfo> ListBackups(string folder)
    {
        string root = Path.Combine(folder, "CharacterClonerBackups");
        if (!Directory.Exists(root)) return [];
        return Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new BackupInfo(path, new DateTimeOffset(Directory.GetLastWriteTimeUtc(path)), Directory.GetFiles(path, "*.sav", SearchOption.TopDirectoryOnly).Length))
            .Where(backup => backup.FileCount > 0)
            .OrderByDescending(backup => backup.CreatedAt)
            .ToList();
    }

    public static string RestoreSelectedBackup(string folder, string backupFolder)
    {
        string backupRoot = Path.GetFullPath(Path.Combine(folder, "CharacterClonerBackups"));
        string selected = Path.GetFullPath(backupFolder);
        string rootPrefix = Path.TrimEndingDirectorySeparator(backupRoot) + Path.DirectorySeparatorChar;
        if (!selected.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("La sauvegarde sélectionnée ne fait pas partie de ce dossier de sauvegardes.");
        Dictionary<string, string> files = BackupFiles(selected);
        if (!files.ContainsKey("profile.sav")) throw new InvalidOperationException("La sauvegarde sélectionnée ne contient pas profile.sav.");
        string safetyBackup = CreateBackup(folder);
        try
        {
            ReplaceSavFiles(selected, folder);
            ValidateSavSnapshot(selected, folder);
            WriteState(folder, new InstanceState
            {
                CreatedAt = DateTimeOffset.Now,
                SourceSlot = -1,
                TargetSlot = -1,
                BackupFolder = safetyBackup,
                ResultProfileHash = Hash(Path.Combine(folder, "profile.sav"))
            });
            return safetyBackup;
        }
        catch
        {
            ReplaceSavFiles(safetyBackup, folder);
            ValidateSavSnapshot(safetyBackup, folder);
            throw;
        }
    }

    public static InstanceState? ReadState(string folder)
    {
        string path = Path.Combine(folder, StateFileName);
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<InstanceState>(File.ReadAllText(path)); }
        catch { return null; }
    }

    public static string Hash(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static ArrayProperty Characters(SaveFile save) => (ArrayProperty)save.SaveData.Objects[0].Properties!.Properties.Single(x => x.Key == "Characters").Value.Value!;

    private static int Integer(PropertyBag properties, string key)
    {
        Property? property = properties.Properties.FirstOrDefault(x => x.Key == key).Value;
        return property?.Value is int value ? value : 0;
    }

    private static int? CharacterId(PropertyBag properties)
    {
        Property? property = properties.Properties.FirstOrDefault(x => x.Key == "ID").Value;
        return property?.Value is int value ? value : null;
    }

    private static string UnrealName(UObject character)
    {
        FName? name = character.LoadedData?.Name;
        return name is null ? "—" : $"{name.Name} #{name.Number?.ToString() ?? "—"}";
    }

    private static WorldInfo ReadWorldInfo(string worldPath)
    {
        if (!File.Exists(worldPath)) return new WorldInfo(null, null, Ui.T("Missing", "Absent"));
        DateTimeOffset updated = new(File.GetLastWriteTimeUtc(worldPath));
        try
        {
            SaveFile world = SaveFile.Read(worldPath);
            PropertyBag properties = world.SaveData.Objects[0].Properties ?? throw new InvalidOperationException();
            string location = StringProperty(properties, "QuestLabel");
            double? seconds = NumericProperty(properties, "TimePlayed");
            return new WorldInfo(updated, seconds is double value && value >= 0 ? TimeSpan.FromSeconds(value) : null, string.IsNullOrWhiteSpace(location) ? Ui.T("Unknown", "Inconnu") : location);
        }
        catch
        {
            return new WorldInfo(updated, null, Ui.T("Unreadable world", "Monde illisible"));
        }
    }

    private static string StringProperty(PropertyBag properties, string key)
    {
        Property? property = properties.Properties.FirstOrDefault(x => x.Key == key).Value;
        return property?.ToStringValue() ?? "";
    }

    private static double? NumericProperty(PropertyBag properties, string key)
    {
        Property? property = properties.Properties.FirstOrDefault(x => x.Key == key).Value;
        return property?.Value switch
        {
            byte value => value,
            short value => value,
            int value => value,
            long value => value,
            float value => value,
            double value => value,
            decimal value => (double)value,
            _ => null
        };
    }

    private static bool HasExpectedId(PropertyBag properties, int slotIndex) => slotIndex == 0 ? CharacterId(properties) is null or 0 : CharacterId(properties) == slotIndex;

    private static string Asset(PropertyBag properties, string key)
    {
        Property? property = properties.Properties.FirstOrDefault(x => x.Key == key).Value;
        if (property?.Value is not string value) return "";
        string name = value[(value.LastIndexOfAny("/.".ToCharArray()) + 1)..];
        return name.Replace("_UI_C", "").Replace("_UI", "").Replace("_C", "").Replace('_', ' ');
    }

    private static string CreateBackup(string folder)
    {
        string root = Path.Combine(folder, "CharacterClonerBackups");
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string backup = Path.Combine(root, timestamp);
        int suffix = 1;
        while (Directory.Exists(backup)) backup = Path.Combine(root, $"{timestamp}-{suffix++}");
        Directory.CreateDirectory(backup);
        foreach (string file in Directory.GetFiles(folder, "*.sav", SearchOption.TopDirectoryOnly)) File.Copy(file, Path.Combine(backup, Path.GetFileName(file)), false);
        return backup;
    }

    private static Dictionary<string, string> BackupFiles(string backupFolder)
    {
        if (!Directory.Exists(backupFolder)) throw new InvalidOperationException("La sauvegarde sélectionnée est introuvable.");
        return Directory.GetFiles(backupFolder, "*.sav", SearchOption.TopDirectoryOnly)
            .ToDictionary(file => Path.GetFileName(file)!, StringComparer.OrdinalIgnoreCase);
    }

    private static CharacterArchiveManifest ReadArchiveManifest(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.GetEntry("manifest.json") ?? throw new InvalidOperationException("L'archive ne contient pas de manifeste.");
        using Stream stream = entry.Open();
        CharacterArchiveManifest? manifest = JsonSerializer.Deserialize<CharacterArchiveManifest>(stream);
        if (manifest is null || manifest.Format != "RemnantCharacterCloner.CharacterArchive" || manifest.Version != 1)
            throw new InvalidOperationException("Le format de l'archive n'est pas reconnu.");
        return manifest;
    }

    private static void ExtractArchiveEntry(ZipArchiveEntry entry, string destination)
    {
        using Stream source = entry.Open();
        using FileStream target = File.Create(destination);
        source.CopyTo(target);
    }

    private static void ReplaceSavFiles(string sourceFolder, string destinationFolder)
    {
        Dictionary<string, string> sourceFiles = BackupFiles(sourceFolder);
        foreach (string current in Directory.GetFiles(destinationFolder, "*.sav", SearchOption.TopDirectoryOnly))
            if (!sourceFiles.ContainsKey(Path.GetFileName(current))) DeleteIfExists(current);
        foreach ((string name, string source) in sourceFiles) File.Copy(source, Path.Combine(destinationFolder, name), true);
    }

    private static void ValidateSavSnapshot(string sourceFolder, string destinationFolder)
    {
        Dictionary<string, string> sourceFiles = BackupFiles(sourceFolder);
        Dictionary<string, string> destinationFiles = BackupFiles(destinationFolder);
        if (!sourceFiles.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(destinationFiles.Keys))
            throw new InvalidOperationException("La restauration n'a pas reproduit la liste complète des fichiers .sav.");
        foreach ((string name, string source) in sourceFiles)
        {
            string destination = destinationFiles[name];
            if (new FileInfo(source).Length != new FileInfo(destination).Length || !Hash(source).Equals(Hash(destination), StringComparison.Ordinal))
                throw new InvalidOperationException($"Le fichier restauré {name} n'a pas passé la vérification.");
        }
    }

    private static void RestoreBackup(string backup, string folder)
    {
        foreach (string file in Directory.GetFiles(backup, "*.sav", SearchOption.TopDirectoryOnly)) File.Copy(file, Path.Combine(folder, Path.GetFileName(file)), true);
    }

    private static void WriteState(string folder, InstanceState state)
    {
        string path = Path.Combine(folder, StateFileName);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }

    private static void ValidateProfile(string path, int sourceSlot, int targetSlot)
    {
        SaveFile verify = SaveFile.Read(path);
        List<ObjectProperty> slots = Characters(verify).Items.Select(x => (ObjectProperty)x!).ToList();
        ObjectProperty sourceSlotProperty = slots[sourceSlot];
        ObjectProperty targetSlotProperty = slots[targetSlot];
        UObject source = sourceSlotProperty.Object ?? throw new InvalidOperationException("Le personnage source est introuvable après vérification.");
        UObject target = targetSlotProperty.Object ?? throw new InvalidOperationException("Le profil généré n'a pas passé la vérification.");
        HashSet<int> sourceGraph = CollectObjectGraph(verify.SaveData, sourceSlotProperty.ObjectIndex);
        HashSet<int> targetGraph = CollectObjectGraph(verify.SaveData, targetSlotProperty.ObjectIndex);
        if (sourceGraph.Overlaps(targetGraph)) throw new InvalidOperationException("La copie partage encore des données internes avec le personnage source.");
        ValidateDistinctObjectNames(source, target);
        PropertyBag properties = target.Properties ?? throw new InvalidOperationException("Le personnage copié ne contient pas de propriétés.");
        Property? id = properties.Properties.FirstOrDefault(x => x.Key == "ID").Value;
        if (id?.Value is not int value || value != targetSlot) throw new InvalidOperationException("L'identifiant de la copie n'a pas passé la vérification.");
    }

    private static void ValidateImportedProfile(string path, int targetSlot)
    {
        SaveFile verify = SaveFile.Read(path);
        List<ObjectProperty> slots = Characters(verify).Items.Select(x => (ObjectProperty)x!).ToList();
        ObjectProperty target = slots[targetSlot];
        if (target.Object is null) throw new InvalidOperationException("Le personnage importé est introuvable après vérification.");
        PropertyBag properties = target.Object.Properties ?? throw new InvalidOperationException("Le personnage importé ne contient pas de propriétés.");
        if (!HasExpectedId(properties, targetSlot)) throw new InvalidOperationException("L'ID du personnage importé n'a pas passé la vérification.");
    }

    private static void ValidateSharedCharacterRecovery(string path, int sourceSlot, int targetSlot, int originalRoot)
    {
        SaveFile verify = SaveFile.Read(path);
        List<ObjectProperty> slots = Characters(verify).Items.Select(x => (ObjectProperty)x!).ToList();
        ObjectProperty source = slots[sourceSlot];
        ObjectProperty target = slots[targetSlot];
        if (source.Object is null || target.Object is null) throw new InvalidOperationException("Un personnage est introuvable après vérification.");
        if (source.ObjectIndex != originalRoot || target.ObjectIndex == source.ObjectIndex)
            throw new InvalidOperationException("Les deux slots pointent encore vers la même entrée de personnage.");
        if (!string.Equals(source.ClassName, target.ClassName, StringComparison.Ordinal))
            throw new InvalidOperationException("La nouvelle entrée de personnage ne correspond pas au personnage source.");
        ValidateDistinctObjectNames(source.Object, target.Object);
        PropertyBag properties = target.Object.Properties ?? throw new InvalidOperationException("Le personnage reconstruit ne contient pas de propriétés.");
        if (!HasExpectedId(properties, targetSlot)) throw new InvalidOperationException("L'ID du personnage reconstruit n'a pas passé la vérification.");
        if (CollectObjectGraph(verify.SaveData, target.ObjectIndex).Contains(source.ObjectIndex))
            throw new InvalidOperationException("Le personnage reconstruit référence encore la racine du personnage source.");
    }

    private static Dictionary<string, string> WorldHashes(string folder)
    {
        return Directory.GetFiles(folder, "save_*.sav", SearchOption.TopDirectoryOnly)
            .ToDictionary(path => Path.GetFileName(path)!, Hash, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateWorldsUnchanged(string folder, Dictionary<string, string> expected)
    {
        Dictionary<string, string> current = WorldHashes(folder);
        if (!expected.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(current.Keys))
            throw new InvalidOperationException("La correction a modifié la liste des fichiers monde, ce qui est interdit.");
        foreach ((string name, string hash) in expected)
            if (!current.TryGetValue(name, out string? currentHash) || !hash.Equals(currentHash, StringComparison.Ordinal))
                throw new InvalidOperationException($"La correction a modifié {name}, ce qui est interdit.");
    }

    private static void ValidateDeletedSlot(string path, int slotIndex)
    {
        SaveFile verify = SaveFile.Read(path);
        List<ObjectProperty> slots = Characters(verify).Items.Select(x => (ObjectProperty)x!).ToList();
        if (slots[slotIndex].Object is not null || slots[slotIndex].ObjectIndex != -1)
            throw new InvalidOperationException("Le slot supprimé n'a pas passé la vérification.");
    }

    private static void SetFallbackActiveSlot(SaveFile save, int removedSlot)
    {
        PropertyBag root = save.SaveData.Objects[0].Properties ?? throw new InvalidOperationException("Le profil ne contient pas de propriétés racine.");
        Property? active = root.Properties.FirstOrDefault(x => x.Key == "ActiveCharacterIndex").Value;
        if (active?.Value is not int activeSlot || activeSlot != removedSlot) return;
        ArrayProperty characters = Characters(save);
        int fallback = characters.Items
            .Select((item, index) => (item, index))
            .Where(x => x.index != removedSlot && (x.item as ObjectProperty)?.Object is not null)
            .Select(x => x.index)
            .DefaultIfEmpty(0)
            .First();
        active.Value = fallback;
    }

    private static void SetCharacterSlotId(UObject character, SaveData profile, int slotId)
    {
        PropertyBag properties = character.Properties ?? throw new InvalidOperationException("Le personnage ne contient pas de propriétés.");
        Property? id = properties.Properties.FirstOrDefault(x => x.Key == "ID").Value;
        if (id is not null)
        {
            id.Value = slotId;
            return;
        }
        EnsureName(profile, "ID");
        EnsureName(profile, "IntProperty");
        Property created = new()
        {
            Name = FName.Create("ID", profile.NamesTable),
            Type = FName.Create("IntProperty", profile.NamesTable),
            Size = 4,
            Index = 0,
            HasPropertyGuid = 0,
            Value = slotId
        };
        properties.Properties.Add(new KeyValuePair<string, Property>("ID", created));
        properties.RefreshLookup();
    }

    private static void EnsureUniqueObjectName(UObject character, SaveData profile)
    {
        if (character.LoadedData?.Name is not { } name)
            throw new InvalidOperationException("Le personnage copié ne contient pas de nom interne Unreal.");
        HashSet<int?> used = profile.Objects
            .Where(item => !ReferenceEquals(item, character) && item.LoadedData?.Name.Name == name.Name)
            .Select(item => item.LoadedData!.Name.Number)
            .ToHashSet();
        int candidate = name.Number is int number && number >= 0 && number < int.MaxValue ? number + 1 : 1;
        while (used.Contains(candidate))
        {
            if (candidate == int.MaxValue) candidate = 1;
            else candidate++;
        }
        name.Number = candidate;
    }

    private static void ValidateDistinctObjectNames(UObject source, UObject target)
    {
        if (source.LoadedData?.Name is not { } sourceName || target.LoadedData?.Name is not { } targetName)
            throw new InvalidOperationException("Un personnage ne contient pas de nom interne Unreal.");
        if (sourceName.Name == targetName.Name && sourceName.Number == targetName.Number)
            throw new InvalidOperationException("Les deux personnages partagent encore le même nom interne Unreal.");
    }

    private static void EnsureName(SaveData profile, string name)
    {
        if (!profile.NamesTable.Contains(name, StringComparer.Ordinal)) profile.NamesTable.Add(name);
    }

    private static HashSet<int> CollectObjectGraph(SaveData saveData, int rootIndex)
    {
        HashSet<int> visited = [];
        Queue<int> pending = new();
        pending.Enqueue(rootIndex);
        while (pending.TryDequeue(out int index))
        {
            if (!visited.Add(index)) continue;
            if (index < 0 || index >= saveData.Objects.Count) throw new InvalidOperationException("Référence de personnage invalide.");
            Visit(saveData.Objects[index], value =>
            {
                if (value is ObjectProperty property && property.ObjectIndex >= 0) pending.Enqueue(property.ObjectIndex);
            }, true);
        }
        return visited;
    }

    private static void RemapObjectReferences(object root, IReadOnlyDictionary<int, int> remap)
    {
        Visit(root, value =>
        {
            if (value is not ObjectProperty property) return;
            if (remap.TryGetValue(property.ObjectIndex, out int replacement)) property.ObjectIndex = replacement;
            property.Object = null;
        }, true);
    }

    private static void ClearCachedObjectReferences(object root)
    {
        Visit(root, value =>
        {
            if (value is ObjectProperty property) property.Object = null;
        }, true);
    }

    private static void Visit(object? root, Action<object> action, bool skipNestedSaveData)
    {
        HashSet<object> seen = new(ReferenceEqualityComparer.Instance);
        void Walk(object? value)
        {
            if (value is null || value is string) return;
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) return;
            action(value);
            if (!type.IsValueType && !seen.Add(value)) return;
            if (skipNestedSaveData && value is SaveData) return;
            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary) { Walk(entry.Key); Walk(entry.Value); }
                return;
            }
            if (value is IEnumerable enumerable)
            {
                foreach (object? item in enumerable) Walk(item);
                return;
            }
            if (IsKeyValuePair(type))
            {
                Walk(type.GetProperty("Key")!.GetValue(value));
                Walk(type.GetProperty("Value")!.GetValue(value));
                return;
            }
            foreach (FieldInfo field in Fields(type)) if (!field.IsNotSerialized) Walk(field.GetValue(value));
        }
        Walk(root);
    }

    private static object? DeepClone(object? value, IDictionary<object, object> seen)
    {
        if (value is null || value is string) return value;
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum) return value;
        if (IsKeyValuePair(type))
        {
            object? key = type.GetProperty("Key")!.GetValue(value);
            object? item = type.GetProperty("Value")!.GetValue(value);
            return Activator.CreateInstance(type, DeepClone(key, seen), DeepClone(item, seen));
        }
        if (type.IsValueType) return value;
        if (seen.TryGetValue(value, out object? existing)) return existing;
        if (value is Array array)
        {
            Array copy = Array.CreateInstance(type.GetElementType()!, array.Length);
            seen.Add(value, copy);
            for (int i = 0; i < array.Length; i++) copy.SetValue(DeepClone(array.GetValue(i), seen), i);
            return copy;
        }
        if (value is IDictionary dictionary)
        {
            IDictionary copy = (IDictionary)Activator.CreateInstance(type)!;
            seen.Add(value, copy);
            foreach (DictionaryEntry entry in dictionary) copy.Add(DeepClone(entry.Key, seen), DeepClone(entry.Value, seen));
            return copy;
        }
        if (value is IList list)
        {
            IList copy = (IList)Activator.CreateInstance(type)!;
            seen.Add(value, copy);
            foreach (object? item in list) copy.Add(DeepClone(item, seen));
            return copy;
        }
        object copyObject = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(value, null)!;
        seen.Add(value, copyObject);
        foreach (FieldInfo field in Fields(type))
        {
            if (field.IsInitOnly) continue;
            if (field.IsNotSerialized)
            {
                field.SetValue(copyObject, null);
                continue;
            }
            field.SetValue(copyObject, DeepClone(field.GetValue(value), seen));
        }
        return copyObject;
    }

    private static IEnumerable<FieldInfo> Fields(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
            foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (!field.IsStatic) yield return field;
    }

    private static bool IsKeyValuePair(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
