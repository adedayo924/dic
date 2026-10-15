namespace DIC.ELibrary.Core.Options;

public class PackageOptions
{
    public const string SectionName = "Package";

    /// <summary>Extension used for book packages.</summary>
    public string Extension { get; set; } = ".dicpkg";

    /// <summary>Extension used for catalog/license bundles.</summary>
    public string CatalogExtension { get; set; } = ".dicl";

    /// <summary>Format version written into package manifests.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>When true, package content is encrypted with AES-256-GCM.</summary>
    public bool Encrypt { get; set; } = true;

    /// <summary>When true, package manifest is signed with the admin RSA key.</summary>
    public bool Sign { get; set; } = true;
}
