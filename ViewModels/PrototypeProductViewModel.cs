namespace ILLiasu_Local_Mall.ViewModels;

// Presentation-only sample fixture. This is not a catalogue entity or a live price.
public sealed record PrototypeProductViewModel(string Name, string Category, string Unit, decimal SamplePrice, string Artwork, string ImageAlt, bool SampleAvailable = true);
