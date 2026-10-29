using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using lib.remnant2.saves.Model;
using lib.remnant2.saves.Model.Properties;

try
{
if (args.Length != 3) throw new ArgumentException("Usage: FinalValidation <valid-profile> <invalid-profile> <output-root>");

string validProfile = Path.GetFullPath(args[0]);
string invalidProfile = Path.GetFullPath(args[1]);
string root = Path.Combine(Path.GetFullPath(args[2]), DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
Directory.CreateDirectory(root);

Assembly app = Assembly.Load("RemnantCharacterCloner");
Type operations = app.GetType("RemnantCharacterCloner.SaveOperations", true)!;

object? Invoke(string name, params object?[] parameters)
{
    MethodInfo method = operations.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(x => x.Name == name && x.GetParameters().Length == parameters.Length);
    try
    {
        return method.Invoke(null, parameters);
    }
    catch (TargetInvocationException ex) when (ex.InnerException is not null)
    {
        ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        throw;
    }
}

void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

IList Slots(string folder) => (IList)Invoke("ReadSlots", folder)!;

object Slot(IList slots, int index) => slots.Cast<object>().Single(x => (int)x.GetType().GetProperty("Index")!.GetValue(x)! == index);

bool Boolean(object item, string name) => (bool)item.GetType().GetProperty(name)!.GetValue(item)!;

int? Integer(object item, string name) => (int?)item.GetType().GetProperty(name)!.GetValue(item);

string Text(object item, string name) => (string)item.GetType().GetProperty(name)!.GetValue(item)!;

TimeSpan? Duration(object item, string name) => (TimeSpan?)item.GetType().GetProperty(name)!.GetValue(item);

string Hash(string path)
{
    using SHA256 sha = SHA256.Create();
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(sha.ComputeHash(stream));
}

int ActiveSlot(string profile)
{
    SaveFile save = SaveFile.Read(profile);
    Property active = save.SaveData.Objects[0].Properties!.Properties.Single(x => x.Key == "ActiveCharacterIndex").Value;
    return (int)active.Value!;
}

(int slotId, int objectNameNumber) CharacterIdentity(string profile, int slot)
{
    SaveFile save = SaveFile.Read(profile);
    ArrayProperty characters = (ArrayProperty)save.SaveData.Objects[0].Properties!.Properties.Single(x => x.Key == "Characters").Value.Value!;
    UObject character = ((ObjectProperty)characters.Items[slot]!).Object!;
    int slotId = (int)character.Properties!.Properties.Single(x => x.Key == "ID").Value.Value!;
    int objectNameNumber = character.LoadedData!.Name.Number!.Value;
    return (slotId, objectNameNumber);
}

IEnumerable<Control> Controls(Control rootControl)
{
    foreach (Control child in rootControl.Controls)
    {
        yield return child;
        foreach (Control nested in Controls(child)) yield return nested;
    }
}

Point Absolute(Control control)
{
    int x = control.Left;
    int y = control.Top;
    for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
    {
        x += parent.Left;
        y += parent.Top;
    }
    return new Point(x, y);
}

string cloneFolder = Path.Combine(root, "clone-delete-restore");
Directory.CreateDirectory(cloneFolder);

string worldReadFolder = Path.Combine(Environment.CurrentDirectory, "work", "live-save-investigation-20261028");
if (File.Exists(Path.Combine(worldReadFolder, "profile.sav")))
{
    IList worldReadSlots = Slots(worldReadFolder);
    object worldReadSlot = Slot(worldReadSlots, 0);
    Assert(!string.IsNullOrWhiteSpace(Text(worldReadSlot, "WorldLocation")), "La lecture de l'emplacement du monde a échoué.");
    Assert(Duration(worldReadSlot, "WorldPlayTime") is not null, "La lecture du temps de jeu a échoué.");
    Assert(!string.IsNullOrWhiteSpace(Text(worldReadSlot, "UnrealName")), "La lecture du nom interne Unreal a échoué.");
}
File.Copy(validProfile, Path.Combine(cloneFolder, "profile.sav"));
File.Copy(validProfile, Path.Combine(cloneFolder, "save_1.sav"));
File.Copy(invalidProfile, Path.Combine(cloneFolder, "save_4.sav"));

string cloneBackup = (string)Invoke("Clone", cloneFolder, 1, 4, true, true)!;
IList cloned = Slots(cloneFolder);
Assert(Boolean(Slot(cloned, 1), "Occupied"), "Le personnage source a disparu après clonage.");
Assert(Boolean(Slot(cloned, 4), "Occupied"), "Le personnage cible est absent après clonage.");
Assert(!Boolean(Slot(cloned, 4), "NeedsRepair"), "Le personnage cloné a un ID invalide.");
Assert(Integer(Slot(cloned, 4), "CharacterId") == 4, "Le personnage cloné n'a pas reçu l'ID 4.");
Assert(CharacterIdentity(Path.Combine(cloneFolder, "profile.sav"), 1).objectNameNumber != CharacterIdentity(Path.Combine(cloneFolder, "profile.sav"), 4).objectNameNumber, "Le personnage cloné réutilise le même nom interne Unreal.");
Assert(Hash(Path.Combine(cloneFolder, "save_1.sav")) == Hash(Path.Combine(cloneFolder, "save_4.sav")), "Le monde source n'a pas été copié exactement.");
Assert(Directory.Exists(cloneBackup), "Le backup de clonage est absent.");

string deleteBackup = (string)Invoke("DeleteSlot", cloneFolder, 1)!;
IList deleted = Slots(cloneFolder);
Assert(!Boolean(Slot(deleted, 1), "Occupied"), "Le personnage supprimé est encore présent.");
Assert(!File.Exists(Path.Combine(cloneFolder, "save_1.sav")), "Le monde supprimé est encore présent.");
Assert(ActiveSlot(Path.Combine(cloneFolder, "profile.sav")) != 1, "Le slot actif pointe vers le personnage supprimé.");
File.Copy(validProfile, Path.Combine(cloneFolder, "save_0.sav"));

string safetyBackup = (string)Invoke("RestoreSelectedBackup", cloneFolder, deleteBackup)!;
IList restored = Slots(cloneFolder);
Assert(Boolean(Slot(restored, 1), "Occupied"), "La restauration n'a pas rétabli le personnage supprimé.");
Assert(File.Exists(Path.Combine(cloneFolder, "save_1.sav")), "La restauration n'a pas rétabli le monde supprimé.");
Assert(!File.Exists(Path.Combine(cloneFolder, "save_0.sav")), "La restauration a conservé un fichier absent du snapshot.");
Assert(ActiveSlot(Path.Combine(cloneFolder, "profile.sav")) == 1, "La restauration n'a pas rétabli le slot actif.");
Assert(Directory.Exists(safetyBackup), "Le backup de sécurité avant restauration est absent.");

string recoveryFolder = Path.Combine(root, "recovery-keeps-worlds");
Directory.CreateDirectory(recoveryFolder);
string recoveryProfile = Path.Combine(recoveryFolder, "profile.sav");
File.Copy(validProfile, recoveryProfile);
File.Copy(invalidProfile, Path.Combine(recoveryFolder, "save_0.sav"));
File.Copy(validProfile, Path.Combine(recoveryFolder, "save_1.sav"));
SaveFile recoverySave = SaveFile.Read(recoveryProfile);
ArrayProperty recoveryCharacters = (ArrayProperty)recoverySave.SaveData.Objects[0].Properties!.Properties.Single(x => x.Key == "Characters").Value.Value!;
ObjectProperty recoverySource = (ObjectProperty)recoveryCharacters.Items[0]!;
ObjectProperty recoveryTarget = (ObjectProperty)recoveryCharacters.Items[1]!;
recoveryTarget.ObjectIndex = recoverySource.ObjectIndex;
recoveryTarget.Object = null;
SaveFile.Write(recoveryProfile, recoverySave);
string world0BeforeRecovery = Hash(Path.Combine(recoveryFolder, "save_0.sav"));
string world1BeforeRecovery = Hash(Path.Combine(recoveryFolder, "save_1.sav"));
int objectsBeforeRecovery = recoverySave.SaveData.Objects.Count;
string recoveryBackup = (string)Invoke("RecoverSharedSlot", recoveryFolder, 0, 1)!;
IList recovered = Slots(recoveryFolder);
Assert(Boolean(Slot(recovered, 0), "Occupied") && Boolean(Slot(recovered, 1), "Occupied"), "La récupération n'a pas conservé les deux personnages.");
Assert(Integer(Slot(recovered, 1), "CharacterId") == 1, "Le personnage récupéré n'a pas reçu l'ID 1.");
Assert(CharacterIdentity(recoveryProfile, 0).slotId == 0 && CharacterIdentity(recoveryProfile, 1).slotId == 1, "La récupération n'a pas fixé les IDs des deux slots.");
Assert(CharacterIdentity(recoveryProfile, 0).objectNameNumber != CharacterIdentity(recoveryProfile, 1).objectNameNumber, "La récupération réutilise le même nom interne Unreal.");
Assert(Hash(Path.Combine(recoveryFolder, "save_0.sav")) == world0BeforeRecovery && Hash(Path.Combine(recoveryFolder, "save_1.sav")) == world1BeforeRecovery, "La récupération a modifié un monde.");
SaveFile recoveredSave = SaveFile.Read(recoveryProfile);
ArrayProperty recoveredCharacters = (ArrayProperty)recoveredSave.SaveData.Objects[0].Properties!.Properties.Single(x => x.Key == "Characters").Value.Value!;
Assert(((ObjectProperty)recoveredCharacters.Items[0]!).ObjectIndex != ((ObjectProperty)recoveredCharacters.Items[1]!).ObjectIndex, "La récupération laisse les deux slots sur la même racine.");
Assert(recoveredSave.SaveData.Objects.Count == objectsBeforeRecovery + 1, "La récupération a dupliqué plus que la racine du personnage.");
Assert(Directory.Exists(recoveryBackup), "Le backup de récupération est absent.");

string archiveFolder = Path.Combine(root, "archive");
Directory.CreateDirectory(archiveFolder);
File.Copy(validProfile, Path.Combine(archiveFolder, "profile.sav"));
File.Copy(validProfile, Path.Combine(archiveFolder, "save_1.sav"));
string archivePath = Path.Combine(root, "slot-2.r2character");
Invoke("ExportCharacter", archiveFolder, 1, archivePath);
object archiveInfo = Invoke("ReadCharacterArchive", archivePath)!;
Assert((bool)archiveInfo.GetType().GetProperty("HasWorld")!.GetValue(archiveInfo)!, "L'archive n'a pas conservé le monde.");
Invoke("ImportCharacter", archiveFolder, archivePath, 4, true);
IList imported = Slots(archiveFolder);
Assert(Boolean(Slot(imported, 4), "Occupied") && !Boolean(Slot(imported, 4), "NeedsRepair"), "Le personnage importé est invalide.");
Assert(Hash(Path.Combine(archiveFolder, "save_1.sav")) == Hash(Path.Combine(archiveFolder, "save_4.sav")), "Le monde de l'archive n'a pas été importé.");

object backups = Invoke("ListBackups", cloneFolder)!;
Type pickerType = app.GetType("RemnantCharacterCloner.BackupPickerForm", true)!;
using Form picker = (Form)Activator.CreateInstance(pickerType, backups)!;
Assert(pickerType.GetProperty("SelectedBackup")!.GetValue(picker) is not null, "Le sélecteur de backup ne présélectionne rien.");

Type mainType = app.GetType("RemnantCharacterCloner.MainForm", true)!;
using Form main = (Form)Activator.CreateInstance(mainType)!;
main.CreateControl();
main.PerformLayout();
foreach (Control container in Controls(main).Where(x => x.HasChildren)) container.PerformLayout();
List<Control> controls = Controls(main).ToList();
Control folderLabel = controls.Single(x => x is Label && (x.Text == "Dossier de sauvegarde :" || x.Text == "Save folder:"));
Control hintLabel = controls.Single(x => x is Label && (x.Text == "Emplacement habituel :" || x.Text == "Usual location:"));
Control hintLink = controls.Single(x => x is LinkLabel && x.Text.StartsWith("C:\\Users", StringComparison.Ordinal));
Control folderBox = controls.Single(x => x is TextBox);
Control browse = controls.Single(x => x is Button && (x.Text == "Parcourir..." || x.Text == "Browse..."));
Control copyPath = controls.Single(x => x is Button && (x.Text == "Copier le chemin" || x.Text == "Copy path"));
Control clone = controls.Single(x => x is Button && (x.Text == "Copier le personnage" || x.Text == "Copy character"));
Control copyWorld = controls.Single(x => x is Button && (x.Text == "Copier le monde" || x.Text == "Copy world"));
Control recovery = controls.Single(x => x is Button && (x.Text == "Récupération avancée..." || x.Text == "Advanced recovery..."));
Control restore = controls.Single(x => x is Button && (x.Text == "Restaurer une sauvegarde..." || x.Text == "Restore a backup..."));
Control help = controls.Single(x => x is Button && (x.Text == "Comment ça marche" || x.Text == "How it works"));
DataGridView grid = controls.OfType<DataGridView>().Single();
Assert(grid.Columns["unrealName"].HeaderText == "Unreal ID", "La colonne Unreal ID est absente.");
Assert(grid.Columns["playTime"] is not null && grid.Columns["location"] is not null, "Les colonnes de temps de jeu et d'emplacement sont absentes.");
Point folderLabelPoint = Absolute(folderLabel);
Point hintLabelPoint = Absolute(hintLabel);
Point browsePoint = Absolute(browse);
Point copyPathPoint = Absolute(copyPath);
Assert(folderLabelPoint.X == hintLabelPoint.X, "Les deux libellés du chemin ne sont pas alignés à gauche.");
Assert(browsePoint.X == copyPathPoint.X, "Parcourir et Copier le chemin ne sont pas alignés à gauche.");
Console.WriteLine($"ui-folder label={folderLabelPoint.Y}+{folderLabel.Height} field={Absolute(folderBox).Y}+{folderBox.Height}");
Console.WriteLine($"ui-hint label={hintLabelPoint.Y}+{hintLabel.Height} button={copyPathPoint.Y}+{copyPath.Height}");
Assert(Math.Abs((folderLabelPoint.Y + folderLabel.Height / 2) - (Absolute(folderBox).Y + folderBox.Height / 2)) <= 1, "Le libellé du dossier n'est pas centré sur son champ.");
Assert(Math.Abs((hintLabelPoint.Y + hintLabel.Height / 2) - (copyPathPoint.Y + copyPath.Height / 2)) <= 1, "Le libellé de l'emplacement n'est pas centré sur son bouton.");
Assert(((LinkLabel)hintLink).BorderStyle == BorderStyle.FixedSingle, "Le chemin habituel n'est pas visuellement séparé.");
Assert(Absolute(clone).X < Absolute(copyWorld).X, "La copie du monde n'est pas placée à droite de la copie du personnage.");
Assert(Absolute(copyWorld).X < Absolute(help).X && Absolute(help).X < Absolute(restore).X, "Les actions de copie, d'aide et de restauration ne sont pas séparées.");
Assert(Absolute(recovery).Y < Absolute(clone).Y, "La récupération avancée n'est pas placée sous la liste.");
Console.WriteLine($"ui-bottom clone={Absolute(clone).Y} world={Absolute(copyWorld).Y} help={Absolute(help).Y} restore={Absolute(restore).Y}");
Assert(Math.Abs(Absolute(clone).Y - Absolute(help).Y) <= 1 && Math.Abs(Absolute(clone).Y - Absolute(restore).Y) <= 1, "Les actions du bas ne sont pas sur la même ligne.");

Console.WriteLine($"VALIDATION OK: {root}");
Console.WriteLine("clone=OK delete=OK restore=OK recovery-keeps-worlds=OK archive=OK backups=OK ui=OK");
return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
