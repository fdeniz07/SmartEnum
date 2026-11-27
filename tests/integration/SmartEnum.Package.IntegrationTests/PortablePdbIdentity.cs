using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace SmartEnum.Package.IntegrationTests;

internal static class PortablePdbIdentity
{
    public static void AssertDllMatchesPortablePdb(byte[] dllBytes, byte[] pdbBytes)
    {
        Assert.True(pdbBytes.Length >= 4, "Core PDB is too small.");
        Assert.True(pdbBytes.AsSpan(0, 4).SequenceEqual("BSJB"u8));

        using var peReader = new PEReader(new MemoryStream(dllBytes, writable: false));
        (CodeViewDebugDirectoryData CodeView, uint Stamp) debug = ReadCodeView(peReader);
        Guid moduleVersionId = ReadModuleVersionId(peReader);

        using MetadataReaderProvider pdbProvider = MetadataReaderProvider.FromPortablePdbStream(
            new MemoryStream(pdbBytes, writable: false));
        MetadataReader pdbReader = pdbProvider.GetMetadataReader();
        Assert.NotNull(pdbReader.DebugMetadataHeader);
        ImmutableArray<byte> pdbId = pdbReader.DebugMetadataHeader.Id;
        Assert.True(pdbId.Length >= 20, "Portable PDB DebugMetadataHeader.Id is shorter than 20 bytes.");

        var pdbGuid = new Guid(pdbId.AsSpan(0, 16));
        uint pdbStamp = BinaryPrimitives.ReadUInt32LittleEndian(pdbId.AsSpan(16, 4));

        Assert.Equal(debug.CodeView.Guid, pdbGuid);
        Assert.Equal(debug.Stamp, pdbStamp);
        Assert.NotEqual(Guid.Empty, moduleVersionId);
        Assert.NotEqual(moduleVersionId, debug.CodeView.Guid);
    }

    public static Guid ReadModuleVersionId(byte[] dllBytes)
    {
        using var peReader = new PEReader(new MemoryStream(dllBytes, writable: false));
        return ReadModuleVersionId(peReader);
    }

    private static (CodeViewDebugDirectoryData CodeView, uint Stamp) ReadCodeView(PEReader peReader)
    {
        ImmutableArray<DebugDirectoryEntry> entries = peReader.ReadDebugDirectory();
        DebugDirectoryEntry codeViewEntry = Assert.Single(
            entries,
            static entry => entry.Type == DebugDirectoryEntryType.CodeView);
        return (peReader.ReadCodeViewDebugDirectoryData(codeViewEntry), codeViewEntry.Stamp);
    }

    private static Guid ReadModuleVersionId(PEReader peReader)
    {
        MetadataReader metadata = peReader.GetMetadataReader();
        return metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    }
}
