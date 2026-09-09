using System.Collections.Generic;

namespace NetWall.IDE.Localization;

internal static class Strings
{
    public static readonly Dictionary<string, (string Es, string En)> Translations = new()
    {
        // Toolbar tooltips
        ["Tooltip.NewFileShortcut"] = ("Nuevo Archivo (Ctrl+N)", "New File (Ctrl+N)"),
        ["Tooltip.NewFile"] = ("Nuevo Archivo", "New File"),
        ["Tooltip.OpenFolderFirst"] = ("Abre una carpeta primero para crear un archivo", "Open a folder first to create a file"),
        ["Tooltip.OpenFolderShortcut"] = ("Abrir Carpeta (Ctrl+O)", "Open Folder (Ctrl+O)"),
        ["Tooltip.OpenFolder"] = ("Abrir Carpeta", "Open Folder"),
        ["Tooltip.SaveShortcut"] = ("Guardar (Ctrl+S)", "Save (Ctrl+S)"),
        ["Tooltip.SaveAllShortcut"] = ("Guardar Todo (Ctrl+Shift+S)", "Save All (Ctrl+Shift+S)"),
        ["Tooltip.RunActiveFile"] = ("Ejecutar Archivo Activo (F5)", "Run Active File (F5)"),
        ["Tooltip.ToggleSidebar"] = ("Alternar Barra Lateral (Ctrl+B)", "Toggle Sidebar (Ctrl+B)"),
        ["Tooltip.ToggleTerminal"] = ("Alternar Terminal", "Toggle Terminal"),
        ["Tooltip.Settings"] = ("Ajustes", "Settings"),
        ["Tooltip.CollapseAll"] = ("Colapsar Todo", "Collapse All"),
        ["Tooltip.SplitRight"] = ("Dividir a la Derecha", "Split Right"),
        ["Tooltip.OpenInSplit"] = ("Abrir en split", "Open in split"),
        ["Tooltip.Close"] = ("Cerrar", "Close"),
        ["Tooltip.ClearSearch"] = ("Limpiar búsqueda", "Clear search"),
        ["Tooltip.CatColor"] = ("Color del gato", "Cat color"),
        ["Tooltip.ClearTerminal"] = ("Limpiar Terminal", "Clear Terminal"),
        ["Tooltip.CloseTerminalPanel"] = ("Cerrar Panel", "Close Panel"),

        // Watermarks
        ["Watermark.GlobalSearch"] = ("Buscar archivos en el proyecto...", "Search files in project..."),
        ["Watermark.FilterFiles"] = ("Filtrar archivos...", "Filter files..."),
        ["Watermark.ExtensionSearch"] = ("Buscar extensión...", "Search extension..."),
        ["Watermark.TerminalInput"] = ("Escribe un comando y presiona Enter...", "Type a command and press Enter..."),

        // Sidebar / panels
        ["Sidebar.Explorer"] = ("EXPLORADOR", "EXPLORER"),
        ["Sidebar.NoFolderOpen"] = ("Sin carpeta abierta", "No folder open"),
        ["Search.NoMatches"] = ("Sin coincidencias", "No matching files"),
        ["ContextMenu.NewFile"] = ("Nuevo Archivo", "New File"),
        ["ContextMenu.NewFolder"] = ("Nueva Carpeta", "New Folder"),
        ["ContextMenu.Rename"] = ("Renombrar", "Rename"),
        ["ContextMenu.Delete"] = ("Eliminar", "Delete"),
        ["Explorer.NewFolderDefaultName"] = ("Nueva Carpeta", "New Folder"),

        // Welcome screen
        ["Welcome.Subtitle"] = ("Abre una carpeta para empezar a programar", "Open a folder to start coding"),
        ["Welcome.OpenFolderButton"] = ("Abrir Carpeta", "Open Folder"),
        ["Welcome.Hint"] = ("o presiona Ctrl+O", "or press Ctrl+O"),
        ["Welcome.Recents"] = ("RECIENTES", "RECENT"),
        ["NoTab.Subtitle"] = ("Selecciona un archivo del explorador o crea uno nuevo", "Select a file from the explorer or create a new one"),
        ["NoTab.NewFileButton"] = ("Nuevo Archivo", "New File"),

        // Pet colors
        ["Color.Gray"] = ("Gris", "Gray"),
        ["Color.Orange"] = ("Naranja", "Orange"),
        ["Color.White"] = ("Blanco", "White"),

        // Settings panel
        ["Settings.Header"] = ("AJUSTES", "SETTINGS"),
        ["Settings.Tab.Theme"] = ("Tema", "Theme"),
        ["Settings.Tab.Extensions"] = ("Extensiones", "Extensions"),
        ["Settings.Tab.Language"] = ("Idioma", "Language"),
        ["Settings.Extensions.Header"] = ("EXTENSIONES", "EXTENSIONS"),
        ["Settings.Language.Header"] = ("IDIOMA", "LANGUAGE"),
        ["Settings.Language.Description"] = ("Elige el idioma de la interfaz", "Choose the interface language"),
        ["Language.Spanish"] = ("Español", "Español"),
        ["Language.English"] = ("English", "English"),

        // Extensions
        ["Extension.IndentRainbow.Description"] = ("Colorea cada nivel de indentación en el editor", "Colors each indentation level in the editor"),
        ["Extension.DevCat.Description"] = ("Una mascota gatito que vive en la terminal", "A kitten pet that lives in the terminal"),
        ["Extensions.CountFormat"] = ("{0} de {1} activas", "{0} of {1} active"),

        // Status bar
        ["StatusBar.NoFolderOpen"] = ("SIN CARPETA ABIERTA", "NO FOLDER OPENED"),

        // Dialogs
        ["Dialog.OpenFolderTitle"] = ("Abrir Carpeta", "Open Folder"),
        ["Dialog.SaveFileTitle"] = ("Guardar Archivo", "Save File"),
        ["Dialog.ConfirmTitle"] = ("Confirmar", "Confirm"),
        ["Dialog.Cancel"] = ("Cancelar", "Cancel"),
        ["Dialog.Delete"] = ("Eliminar", "Delete"),
        ["Dialog.DeleteFolderMessage"] = ("¿Eliminar la carpeta '{0}' y todo su contenido? Esta acción no se puede deshacer.", "Delete the folder '{0}' and everything inside it? This cannot be undone."),
        ["Dialog.DeleteFileMessage"] = ("¿Eliminar '{0}'? Esta acción no se puede deshacer.", "Delete '{0}'? This cannot be undone."),
    };
}
