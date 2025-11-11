using Avalonia.Controls;
using Avalonia.Controls.Templates;
using FlashcardApp.ViewModels;
using FlashcardApp.Views;
using System;
using CommunityToolkit.Mvvm.ComponentModel; // <-- HIER IST DIE KORREKTUR

namespace FlashcardApp
{
    // Diese Klasse ist pures Avalonia-Binding-System.
    // Sie muss nicht im Detail verstanden werden, sie muss nur existieren.
    public class ViewLocator : IDataTemplate
    {
        // Diese Methode wird vom ContentControl aufgerufen
        public Control Build(object? data)
        {
            if (data is null)
            {
                return new TextBlock { Text = "Kein ViewModel gefunden" };
            }

            // Ersetzt "ViewModel" im Namen durch "View"
            // z.B. "FlashcardApp.ViewModels.DeckListViewModel"
            // wird zu "FlashcardApp.Views.DeckListView"
            var name = data.GetType().FullName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);

            if (type != null)
            {
                // Erstellt eine Instanz der View (z.B. new DeckListView())
                return (Control)Activator.CreateInstance(type)!;
            }
            else
            {
                // Fallback, falls die View nicht gefunden wird
                return new TextBlock { Text = "View nicht gefunden: " + name };
            }
        }

        // Diese Methode sagt Avalonia, DASS dieser ViewLocator
        // für das gegebene Objekt zuständig ist.
        public bool Match(object? data)
        {
            // Sagt Avalonia, dass dieser Locator für alle ViewModels zuständig ist
            return data is ObservableObject;
        }
    }
}