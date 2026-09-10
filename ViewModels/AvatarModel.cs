namespace DentistDB.ViewModels;

public sealed record AvatarModel(int PatientId, string Name, bool HasPhoto, string Size = "");
