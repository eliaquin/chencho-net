using System.ComponentModel;
using System.Runtime.CompilerServices;
using Carnets.Core;

namespace Carnets.Desktop;

public sealed class HolderRow(Holder holder) : INotifyPropertyChanged
{
    public Holder Holder { get; } = holder;
    public string Matricula => Holder.Matricula;
    public string Nombre => Holder["nombre"];
    public string Apellido => Holder["apellido"];
    public string Cedula => Holder["cedula"];
    public string Filial => Holder["filial"];
    public string SegundoNombre => Holder["segundoNombre"];
    public string SegundoApellido => Holder["segundoApellido"];
    public string Exequatur => Holder["numeroExequatur"];
    public string Admision => Holder["fechaAdmision"];
    public string Expiracion => Holder["fechaExpiracion"];
    public int Row => Holder.Row;
    private bool selected;
    public bool Selected { get => selected; set { selected = value; Notify(); Changed?.Invoke(); } }
    public Action? Changed { get; set; }
    public string? PhotoPath { get; set; }
    public string? PhotoError { get; set; }
    public PreparedPhoto? Photo { get; set; }
    public Crop Crop { get; set; } = new();
    public List<string> LayoutErrors { get; set; } = [];
    public bool LowResolution { get; set; }
    public bool Valid => Holder.IsValid && PhotoError is null && PhotoPath is not null && LayoutErrors.Count == 0;
    public string Status => !Valid ? "Error" : Photo is null ? "Revisar foto" : Holder.Issues.Any(i => i.Warning) || LowResolution ? "Advertencia" : "Listo";
    public string Details => string.Join("\n", Holder.Issues.Select(i => $"{(i.Warning ? "Advertencia" : "Error")} · {i.Field}: {i.Message}").Concat(PhotoError is null ? [] : new[] { "Foto: " + PhotoError }).Concat(LayoutErrors).Concat(LowResolution ? new[] { "Advertencia: resolución efectiva menor a 150 ppp." } : []).Prepend($"Hoja {Holder.Sheet} · fila {Row}"));
    public void Refresh() { Notify(nameof(Status)); Notify(nameof(Details)); Notify(nameof(Valid)); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
