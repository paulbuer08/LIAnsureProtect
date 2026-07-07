using System.Text;
using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Documents;
using LIAnsureProtect.Modules.Claims.Domain;
using LIAnsureProtect.Platform.Abstractions.Documents;
using Moq;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class ClaimDocumentHandlerTests
{
    private const string ValidSha256 = "a3f5b8c9d2e1f4a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0";
    private static readonly DateTime FiledAtUtc = new(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IClaimRepository> claimRepository = new();
    private readonly Mock<IDocumentStorageService> documentStorage = new();
    private readonly Mock<IClaimDocumentScanner> documentScanner = new();

    private static Claim FileClaim(string ownerUserId = "customer-1")
    {
        var claim = Claim.File(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ownerUserId,
            "CLM-2026-0A1B2C3D",
            ClaimIncidentType.RansomwareExtortion,
            new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 12, 9, 30, 0, DateTimeKind.Utc),
            "Ransomware encrypted the file server.",
            "POL-2026-11111111",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            1_000_000m,
            25_000m,
            FiledAtUtc);
        claim.ClearDomainEvents();

        return claim;
    }

    private static ClaimDocumentUpload PdfUpload(string fileName = "forensic-report.pdf", long sizeBytes = 1024)
        => new(fileName, "application/pdf", sizeBytes, new MemoryStream(Encoding.UTF8.GetBytes("pdf-bytes")));

    private UploadClaimDocumentsCommandHandler CreateUploadHandler(string userId = "customer-1")
    {
        documentStorage
            .Setup(storage => storage.StoreAsync(It.IsAny<DocumentStorageUpload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredDocumentResult($"claims/{Guid.NewGuid():N}"));
        documentStorage
            .Setup(storage => storage.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new StoredDocumentDownload(new MemoryStream(Encoding.UTF8.GetBytes("pdf-bytes")), "application/pdf"));
        documentScanner
            .Setup(scanner => scanner.ScanAsync(It.IsAny<ClaimDocumentScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaimDocumentScanResult(
                ClaimDocumentScanStatus.Clean,
                "TestScanner",
                "NO_THREATS_FOUND",
                "Clean.",
                ValidSha256,
                DateTime.UtcNow));

        return new UploadClaimDocumentsCommandHandler(
            claimRepository.Object,
            new TestClaimsCurrentUser(userId),
            documentStorage.Object,
            documentScanner.Object);
    }

    private void SetUpClaim(Claim? claim)
    {
        claimRepository
            .Setup(repository => repository.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);
    }

    [Fact]
    public async Task Upload_Stores_Scans_And_Persists_Documents()
    {
        var claim = FileClaim();
        SetUpClaim(claim);
        var handler = CreateUploadHandler();

        var result = await handler.Handle(
            new UploadClaimDocumentsCommand(claim.Id, ClaimDocumentKind.ForensicReport, [PdfUpload()]),
            CancellationToken.None);

        Assert.NotNull(result);
        var documentResult = Assert.Single(result!.Documents);
        Assert.Equal("Clean", documentResult.ScanStatus);
        Assert.Equal("forensic-report.pdf", documentResult.OriginalFileName);
        Assert.Single(claim.Documents);
        Assert.Equal(ValidSha256, claim.Documents.Single().Sha256);
        claimRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_Is_Owner_Scoped()
    {
        var claim = FileClaim(ownerUserId: "customer-2");
        SetUpClaim(claim);
        var handler = CreateUploadHandler(userId: "customer-1");

        var result = await handler.Handle(
            new UploadClaimDocumentsCommand(claim.Id, ClaimDocumentKind.Invoice, [PdfUpload()]),
            CancellationToken.None);

        Assert.Null(result);
        claimRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_Requires_At_Least_One_File()
    {
        var claim = FileClaim();
        SetUpClaim(claim);
        var handler = CreateUploadHandler();

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new UploadClaimDocumentsCommand(claim.Id, ClaimDocumentKind.Invoice, []),
            CancellationToken.None));
    }

    [Fact]
    public async Task Upload_Rejects_Too_Many_Files()
    {
        var claim = FileClaim();
        SetUpClaim(claim);
        var handler = CreateUploadHandler();
        var uploads = Enumerable.Range(0, 6).Select(index => PdfUpload($"file-{index}.pdf")).ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new UploadClaimDocumentsCommand(claim.Id, ClaimDocumentKind.Invoice, uploads),
            CancellationToken.None));
    }

    [Fact]
    public async Task Upload_Rejects_Oversize_Files()
    {
        var claim = FileClaim();
        SetUpClaim(claim);
        var handler = CreateUploadHandler();

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new UploadClaimDocumentsCommand(
                claim.Id,
                ClaimDocumentKind.Invoice,
                [PdfUpload(sizeBytes: ClaimDocumentUploadRules.MaximumDocumentSizeBytes + 1)]),
            CancellationToken.None));
    }

    [Fact]
    public async Task Upload_Rejects_Unsupported_Content_Types()
    {
        var claim = FileClaim();
        SetUpClaim(claim);
        var handler = CreateUploadHandler();
        var upload = new ClaimDocumentUpload("script.exe", "application/x-msdownload", 1024, new MemoryStream());

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new UploadClaimDocumentsCommand(claim.Id, ClaimDocumentKind.Other, [upload]),
            CancellationToken.None));
    }

    [Fact]
    public async Task Upload_Rejects_Path_Information_In_File_Names()
    {
        var claim = FileClaim();
        SetUpClaim(claim);
        var handler = CreateUploadHandler();
        var upload = new ClaimDocumentUpload("../escape.pdf", "application/pdf", 1024, new MemoryStream());

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new UploadClaimDocumentsCommand(claim.Id, ClaimDocumentKind.Other, [upload]),
            CancellationToken.None));
    }

    [Fact]
    public async Task Owner_Download_Streams_Only_Clean_Documents()
    {
        var claim = FileClaim();
        var document = claim.AddDocument(
            ClaimDocumentKind.Invoice, "invoice.pdf", "application/pdf", 1024, "claims/key-1", "customer-1", FiledAtUtc.AddHours(1));
        document.RecordScanResult(ClaimDocumentScanStatus.Clean, "TestScanner", "OK", "Clean.", ValidSha256, FiledAtUtc.AddHours(1));
        SetUpClaim(claim);
        documentStorage
            .Setup(storage => storage.OpenReadAsync("claims/key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredDocumentDownload(new MemoryStream(Encoding.UTF8.GetBytes("pdf-bytes")), "application/pdf"));
        var handler = new DownloadOwnerClaimDocumentQueryHandler(
            claimRepository.Object, new TestClaimsCurrentUser("customer-1"), documentStorage.Object);

        var result = await handler.Handle(
            new DownloadOwnerClaimDocumentQuery(claim.Id, document.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("invoice.pdf", result!.OriginalFileName);
    }

    [Fact]
    public async Task Download_Of_An_Unclean_Document_Is_Refused()
    {
        var claim = FileClaim();
        var document = claim.AddDocument(
            ClaimDocumentKind.Invoice, "invoice.pdf", "application/pdf", 1024, "claims/key-1", "customer-1", FiledAtUtc.AddHours(1));
        document.RecordScanResult(ClaimDocumentScanStatus.Rejected, "TestScanner", "THREATS_FOUND", "Marker.", ValidSha256, FiledAtUtc.AddHours(1));
        SetUpClaim(claim);
        var handler = new DownloadOwnerClaimDocumentQueryHandler(
            claimRepository.Object, new TestClaimsCurrentUser("customer-1"), documentStorage.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new DownloadOwnerClaimDocumentQuery(claim.Id, document.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Adjuster_Download_Is_Not_Owner_Scoped_But_Still_Clean_Gated()
    {
        var claim = FileClaim(ownerUserId: "customer-9");
        var document = claim.AddDocument(
            ClaimDocumentKind.Invoice, "invoice.pdf", "application/pdf", 1024, "claims/key-2", "customer-9", FiledAtUtc.AddHours(1));
        document.RecordScanResult(ClaimDocumentScanStatus.Clean, "TestScanner", "OK", "Clean.", ValidSha256, FiledAtUtc.AddHours(1));
        SetUpClaim(claim);
        documentStorage
            .Setup(storage => storage.OpenReadAsync("claims/key-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredDocumentDownload(new MemoryStream(Encoding.UTF8.GetBytes("pdf-bytes")), "application/pdf"));
        var handler = new DownloadAdjudicationClaimDocumentQueryHandler(
            claimRepository.Object, documentStorage.Object);

        var result = await handler.Handle(
            new DownloadAdjudicationClaimDocumentQuery(claim.Id, document.Id), CancellationToken.None);

        Assert.NotNull(result);
    }
}
