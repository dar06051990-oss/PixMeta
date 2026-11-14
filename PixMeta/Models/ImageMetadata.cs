namespace PixMeta.Models;
public sealed class ImageMetadata {
 public string Source {get;set;}="Unknown"; public string Prompt{get;set;}=""; public string NegativePrompt{get;set;}="";
 public string ActualFormat{get;set;}="Unknown"; public bool ExtensionMismatch{get;set;}
 public Dictionary<string,string> Fields{get;}=new(); public string Raw{get;set;}="";
 public string ExportData{get;set;}=""; public string ExportKind{get;set;}="";
 public bool HasGenerationData => Prompt.Length>0 || NegativePrompt.Length>0 || Fields.Count>0;
}
