using System.ComponentModel.DataAnnotations;

namespace CapyCard.Models
{
    public class UserSettings
    {
        [Key]
        public int Id { get; set; } = 1; // Always 1, single row table

        public string ThemeColor { get; set; } = "Teal";
        
        public string ThemeMode { get; set; } = "System"; // System, Light, Dark

        public bool IsZenMode { get; set; } = false;

        public bool ShowEditorToolbar { get; set; } = true;
    }
}
