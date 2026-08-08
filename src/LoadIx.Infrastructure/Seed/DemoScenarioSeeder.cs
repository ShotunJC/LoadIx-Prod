using System.Security.Cryptography;
using System.Text;
using LoadIx.Application.Documents;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Documents;
using LoadIx.Domain.Edge;
using LoadIx.Domain.Finance;
using LoadIx.Domain.Instructions;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Notifications;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Seed;

/// <summary>
/// Builds the fixed, synthetic sales and training story for the demo tenant.
/// The clock and all externally visible identifiers are deliberately stable.
/// </summary>
public sealed class DemoScenarioSeeder(
    LoadIxDbContext db,
    CurrentRequestContext requestContext,
    IObjectStorage objectStorage)
{
    public static readonly DateTimeOffset DemoNowUtc =
        new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        requestContext.Set(DevelopmentSeedIds.TenantId, DevelopmentSeedIds.ActorId);
        if (await db.TransportContracts.AnyAsync(cancellationToken))
        {
            return;
        }

        await db.Database.CreateExecutionStrategy().ExecuteAsync(
            () => SeedWithinTransactionAsync(cancellationToken));
    }

    private async Task SeedWithinTransactionAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var contracts = CreateContracts();
        db.TransportContracts.AddRange(contracts);
        await db.SaveChangesAsync(cancellationToken);

        var instructions = CreateInstructions();
        db.LoadInstructions.AddRange(instructions);
        await db.SaveChangesAsync(cancellationToken);

        var trips = CreateTrips(instructions, contracts);
        db.Trips.AddRange(trips);
        AddNotifications(trips);
        AddEdgeDevice();
        AddFinancials(trips);
        await AddDocumentsAsync(trips, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static TransportContract[] CreateContracts()
    {
        var rateTypes = new[]
        {
            ContractRateType.SourceNetTonne,
            ContractRateType.DestinationNetTonne,
            ContractRateType.FixedPerTrip,
            ContractRateType.BasePlusPerTonne,
        };
        var routeLocations = new[] { (0, 6), (1, 6), (2, 7), (0, 3) };
        var contracts = new TransportContract[4];
        for (var index = 0; index < contracts.Length; index++)
        {
            var routeId = DevelopmentSeedIds.RouteIds[index];
            var type = rateTypes[index];
            var result = TransportContract.Create(
                DevelopmentSeedIds.ContractIds[index],
                DevelopmentSeedIds.TenantId,
                DevelopmentSeedIds.CustomerIds[index],
                $"LXD-2026-{index + 1:000}",
                index switch
                {
                    0 => "Coal export corridor",
                    1 => "Manganese port shuttle",
                    2 => "Scheduled grain distribution",
                    _ => "Aggregate project deliveries",
                },
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                "ZAR",
                index == 2 ? 21 : 30,
                index == 1 ? 0.50m : 1.00m,
                "Synthetic commercial terms for LoadIx demonstrations only.",
                [new ContractRouteDefinition(
                    routeId,
                    DevelopmentSeedIds.LocationIds[routeLocations[index].Item1],
                    DevelopmentSeedIds.LocationIds[routeLocations[index].Item2],
                    $"DEMO-LANE-{index + 1:00}")],
                [DevelopmentSeedIds.CommodityIds[index]],
                [new ContractRateDefinition(
                    DevelopmentSeedIds.RateIds[index], routeId, type,
                    new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
                    type == ContractRateType.FixedPerTrip ? 18_500m : null,
                    type == ContractRateType.BasePlusPerTonne ? 2_750m : null,
                    type == ContractRateType.FixedPerTrip ? null : 485m + index * 35m,
                    type == ContractRateType.BasePlusPerTonne ? 12_000m : null,
                    index is 0 or 3 ? 7.5m : null)]);
            Ensure(result);
            Ensure(result.Value.Activate());
            contracts[index] = result.Value;
        }

        return contracts;
    }

    private static LoadInstruction[] CreateInstructions()
    {
        var instructions = new LoadInstruction[8];
        for (var index = 0; index < instructions.Length; index++)
        {
            var contractIndex = index % 4;
            var collection = DemoNowUtc.AddDays(index - 4).AddHours(-2);
            var result = LoadInstruction.Create(
                DevelopmentSeedIds.InstructionIds[index],
                DevelopmentSeedIds.TenantId,
                new LoadInstructionDefinition(
                    $"LI-DEMO-{index + 1:000}",
                    DevelopmentSeedIds.CustomerIds[contractIndex],
                    DevelopmentSeedIds.ContractIds[contractIndex],
                    DevelopmentSeedIds.RouteIds[contractIndex],
                    DevelopmentSeedIds.CommodityIds[contractIndex],
                    136m,
                    WeightUnit.Tonnes,
                    4,
                    collection,
                    collection.AddHours(4),
                    collection.AddHours(7),
                    collection.AddHours(16),
                    $"PO-DEMO-{index + 1:000}",
                    $"CUSTOMER-REF-{index + 1:000}",
                    index is 0 or 4 ? LoadInstructionPriority.High : LoadInstructionPriority.Normal,
                    "Use the designated site entrance and present the synthetic LoadIx reference."));
            Ensure(result);
            Ensure(result.Value.Plan());
            Ensure(result.Value.GenerateTrips(
                4,
                DevelopmentSeedIds.TripIds.Skip(index * 4).Take(4).ToArray()));
            instructions[index] = result.Value;
        }

        return instructions;
    }

    private static Trip[] CreateTrips(
        LoadInstruction[] instructions,
        TransportContract[] contracts)
    {
        var targets = new[]
        {
            TripStatus.Planned, TripStatus.Planned,
            TripStatus.Assigned, TripStatus.Assigned,
            TripStatus.Dispatched, TripStatus.Dispatched,
            TripStatus.AtCollection, TripStatus.AtCollection,
            TripStatus.Loading, TripStatus.Loading,
            TripStatus.CollectionComplete, TripStatus.CollectionComplete,
            TripStatus.InTransit, TripStatus.InTransit, TripStatus.InTransit, TripStatus.InTransit,
            TripStatus.AtDelivery, TripStatus.AtDelivery,
            TripStatus.Unloading, TripStatus.Unloading,
            TripStatus.DeliveryComplete, TripStatus.DeliveryComplete,
            TripStatus.ReconciliationRequired, TripStatus.ReconciliationRequired,
            TripStatus.ReconciliationRequired, TripStatus.ReconciliationRequired,
            TripStatus.Completed, TripStatus.Completed, TripStatus.Completed,
            TripStatus.Completed, TripStatus.Completed, TripStatus.Completed,
        };
        var trips = new Trip[targets.Length];
        for (var index = 0; index < trips.Length; index++)
        {
            var instructionIndex = index / 4;
            var contractIndex = instructionIndex % 4;
            var instruction = instructions[instructionIndex];
            var contract = contracts[contractIndex];
            var generated = instruction.Trips.OrderBy(item => item.TripNumber).ElementAt(index % 4);
            var rate = contract.Rates.Single();
            var snapshot = TripRateSnapshot.Create(new ContractRateSnapshot(
                rate.Id, rate.RateType, rate.EffectiveFrom, rate.EffectiveTo,
                rate.FixedAmount, rate.BaseFee, rate.PerTonneAmount,
                rate.MinimumCharge, rate.FuelSurchargePercent,
                rate.RoundingDecimalPlaces, rate.RoundingMode, "ZAR"));
            var result = Trip.Create(
                DevelopmentSeedIds.TripIds[index],
                DevelopmentSeedIds.TenantId,
                new TripDefinition(
                    generated.TripNumber,
                    instruction.Id,
                    instruction.CustomerId,
                    instruction.TransportContractId,
                    contract.ContractNumber,
                    "demo-2026-v1",
                    instruction.ContractRouteId,
                    contract.Routes.Single().Reference,
                    instruction.CommodityId,
                    generated.PlannedQuantity,
                    WeightUnit.Tonnes,
                    instruction.CollectionWindowStartUtc.AddMinutes(index % 4 * 45),
                    instruction.DeliveryWindowStartUtc.AddMinutes(index % 4 * 45)),
                snapshot);
            Ensure(result);
            var trip = result.Value;
            AdvanceTrip(trip, targets[index], index);
            trips[index] = trip;
        }

        return trips;
    }

    private static void AdvanceTrip(Trip trip, TripStatus target, int index)
    {
        if (target == TripStatus.Planned) return;
        var at = DemoNowUtc.AddDays(-5).AddMinutes(index * 11);
        TripCommandContext Context(string? reason = null, bool manual = false) =>
            new(DevelopmentSeedIds.ActorId, at = at.AddMinutes(18), reason, manual);
        Ensure(trip.Assign(
            DevelopmentSeedIds.DeterministicGuid(160, index % 20 + 1),
            DevelopmentSeedIds.DeterministicGuid(170, index % 15 + 1),
            [DevelopmentSeedIds.DeterministicGuid(180, index % 25 + 1)], Context()));
        if (target == TripStatus.Assigned) return;
        Ensure(trip.Dispatch(Context()));
        if (target == TripStatus.Dispatched) return;
        Ensure(trip.ConfirmCollectionArrival(Context()));
        if (target == TripStatus.AtCollection) return;
        Ensure(trip.StartLoading(Context()));
        if (target == TripStatus.Loading) return;
        var sourceNet = 33.600m + index % 4 * 0.125m;
        Ensure(trip.RecordSourceWeighbridge(sourceNet + 12.400m, 12.400m, $"SRC-DEMO-{index + 1:0000}"));
        Ensure(trip.CompleteCollection(Context()));
        if (target == TripStatus.CollectionComplete) return;
        Ensure(trip.DepartCollection(Context()));
        if (target == TripStatus.InTransit) return;
        Ensure(trip.ConfirmDeliveryArrival(Context()));
        if (target == TripStatus.AtDelivery) return;
        Ensure(trip.StartUnloading(Context()));
        if (target == TripStatus.Unloading) return;
        var exceptional = index is 23 or 25 or 29;
        var destinationNet = sourceNet - (exceptional ? 1.250m : 0.120m);
        Ensure(trip.RecordDestinationWeighbridge(destinationNet + 12.350m, 12.350m, $"DST-DEMO-{index + 1:0000}"));
        Ensure(trip.CompleteDelivery(Context()));
        if (target == TripStatus.DeliveryComplete) return;
        Ensure(trip.SubmitForReconciliation(Context()));
        if (exceptional)
        {
            Ensure(trip.AddOperationalNote(
                "Reconciliation exception approved for training: sealed load inspected; documented scale variance accepted."));
        }
        if (target == TripStatus.ReconciliationRequired) return;
        Ensure(trip.ConfirmReconciliation());
        trip.ConfirmRequiredDocuments();
        Ensure(trip.Complete(Context()));
    }

    private void AddNotifications(Trip[] trips)
    {
        for (var index = 0; index < 12; index++)
        {
            var trip = trips[20 + index % 12];
            var notificationId = DevelopmentSeedIds.DeterministicGuid(201, index + 1);
            db.NotificationMessages.Add(new NotificationMessage
            {
                Id = notificationId,
                TenantId = DevelopmentSeedIds.TenantId,
                RecipientUserId = DevelopmentSeedIds.ActorId,
                Type = index % 3 == 0 ? "ReconciliationException" : "TripMilestone",
                Title = index % 3 == 0 ? "Weight variance needs review" : "Trip milestone recorded",
                Body = $"{trip.TripNumber}: synthetic demonstration notification.",
                DeduplicationKey = $"demo-notification-{index + 1:00}",
                CreatedAtUtc = DemoNowUtc.AddHours(-index * 3),
                ReadAtUtc = index < 7 ? DemoNowUtc.AddHours(-index * 3 + 1) : null,
            });
            db.NotificationDeliveries.Add(new NotificationDelivery
            {
                Id = DevelopmentSeedIds.DeterministicGuid(202, index + 1),
                NotificationId = notificationId,
                TenantId = DevelopmentSeedIds.TenantId,
                Channel = "InApp",
                Status = "Delivered",
                Attempts = 1,
                ProviderCorrelationId = $"DEMO-DELIVERY-{index + 1:00}",
                NextAttemptAtUtc = DemoNowUtc,
                DeliveredAtUtc = DemoNowUtc.AddHours(-index * 3),
            });
        }
    }

    private void AddFinancials(IEnumerable<Trip> trips)
    {
        var completed = trips.Where(item => item.Status == TripStatus.Completed).ToArray();
        for (var index = 0; index < completed.Length; index++)
        {
            var trip = completed[index];
            var customerName = $"Demo customer {trip.CustomerId.ToString()[^4..]}";
            var financialId = DevelopmentSeedIds.DeterministicGuid(203, index + 1);
            var result = TripFinancials.Create(
                financialId, trip.TenantId, trip.Id, trip.TripNumber,
                trip.CustomerId, customerName, trip.TransportContractId,
                trip.ContractNumber, trip.RateSnapshot,
                trip.DestinationNetWeight ?? trip.PlannedQuantity,
                TaxTreatment.StandardRated, 15m, DemoNowUtc.AddDays(-1));
            Ensure(result);
            var financial = result.Value;
            var cost = CostEntry.Create(
                DevelopmentSeedIds.DeterministicGuid(204, index + 1),
                trip.TenantId, financialId, CostEntryType.FuelEstimate,
                6_800m + index * 125m, "ZAR", "Synthetic fuel and route cost estimate.",
                DevelopmentSeedIds.ActorId, DemoNowUtc.AddDays(-1));
            Ensure(cost);
            Ensure(financial.AddCost(cost.Value));
            Ensure(financial.Approve(
                DevelopmentSeedIds.ActorId, DemoNowUtc.AddHours(-6),
                "Reviewed for demo invoice preparation."));
            db.TripFinancials.Add(financial);
        }
    }

    private async Task AddDocumentsAsync(
        IEnumerable<Trip> trips,
        CancellationToken cancellationToken)
    {
        await objectStorage.EnsureReadyAsync(cancellationToken);
        var completed = trips.Where(item => item.Status == TripStatus.Completed).Take(3).ToArray();
        for (var index = 0; index < completed.Length; index++)
        {
            var trip = completed[index];
            var bytes = DemoPdf.Create(
                "LoadIx synthetic proof of delivery",
                $"Trip {trip.TripNumber} | No signature or personal information | Training use only");
            var documentId = DevelopmentSeedIds.DeterministicGuid(205, index + 1);
            var key = $"{DevelopmentSeedIds.TenantId.Value:N}/demo/{documentId:N}.pdf";
            await using var content = new MemoryStream(bytes, writable: false);
            await objectStorage.PutAsync(key, content, bytes.Length, "application/pdf", cancellationToken);
            var checksum = Convert.ToHexString(SHA256.HashData(bytes));
            var document = OperationalDocument.Create(
                documentId, DevelopmentSeedIds.TenantId, DocumentOwnerType.Trip,
                trip.Id, DocumentType.ProofOfDelivery,
                $"{trip.TripNumber}-synthetic-pod.pdf", "application/pdf",
                bytes.Length, checksum, key, null, DevelopmentSeedIds.ActorId,
                DemoNowUtc.AddHours(-4 - index),
                "Generated by the repository demo PDF generator; contains synthetic data only.");
            Ensure(document);
            db.Documents.Add(document.Value);
        }
    }

    private void AddEdgeDevice()
    {
        var device = EdgeDevice.Create(
            DevelopmentSeedIds.DeterministicGuid(206, 1),
            DevelopmentSeedIds.TenantId,
            "Central Weighbridge Demo Agent",
            DevelopmentSeedIds.LocationIds[5],
            "generic-line",
            "generic-line-v1");
        Ensure(device);
        Ensure(device.Value.Register(DemoNowUtc.AddDays(-14), "loadix-edge-simulator/1.0"));
        Ensure(device.Value.RecordHeartbeat(DemoNowUtc.AddSeconds(-20), "loadix-edge-simulator/1.0", "Healthy"));
        db.EdgeDevices.Add(device.Value);
        var weighbridge = WeighbridgeDevice.Create(
            DevelopmentSeedIds.DeterministicGuid(207, 1),
            DevelopmentSeedIds.TenantId,
            device.Value.Id,
            DevelopmentSeedIds.LocationIds[5],
            "Central inbound scale",
            "WB-DEMO-01",
            "generic-line",
            "generic-line-v1");
        Ensure(weighbridge);
        db.WeighbridgeDevices.Add(weighbridge.Value);

        var credential = EdgeDeviceCredential.Create(
            DevelopmentSeedIds.DeterministicGuid(208, 1),
            DevelopmentSeedIds.TenantId,
            device.Value.Id,
            EdgeDeviceCredentialType.Device,
            "$DEMO$NONUSABLE-HASHED-CREDENTIAL-MATERIAL",
            DemoNowUtc.AddDays(-14));
        Ensure(credential);
        db.EdgeDeviceCredentials.Add(credential.Value);

        var command = EdgeCommand.Create(
            DevelopmentSeedIds.DeterministicGuid(209, 1),
            DevelopmentSeedIds.TenantId,
            device.Value.Id,
            credential.Value.Id,
            "weight-reading",
            "demo-reading-0001",
            "demo-nonce-0001",
            new string('A', 64),
            DemoNowUtc.AddSeconds(-35),
            DemoNowUtc.AddSeconds(-34),
            "demo-edge-correlation-0001");
        Ensure(command);
        var rawReadingId = DevelopmentSeedIds.DeterministicGuid(210, 1);
        command.Value.Succeed(rawReadingId);
        db.EdgeCommands.Add(command.Value);

        var raw = RawWeightReading.Create(
            rawReadingId,
            DevelopmentSeedIds.TenantId,
            device.Value.Id,
            weighbridge.Value.Id,
            45_670m,
            WeightUnit.Kilograms,
            true,
            DemoNowUtc.AddSeconds(-35),
            DemoNowUtc.AddSeconds(-34),
            "DEMO TRK 01 GP",
            "ST,GS,45670,kg",
            "generic-line-v1",
            "demo-edge-correlation-0001",
            command.Value.Id);
        Ensure(raw);
        db.RawWeightReadings.Add(raw.Value);
        var stable = StableWeightReading.Create(
            DevelopmentSeedIds.DeterministicGuid(211, 1),
            DevelopmentSeedIds.TenantId,
            raw.Value.Id,
            device.Value.Id,
            weighbridge.Value.Id,
            raw.Value.DisplayedValue,
            raw.Value.Unit,
            raw.Value.CapturedAtUtc,
            raw.Value.ReceivedAtUtc,
            raw.Value.VehicleRegistration,
            raw.Value.CorrelationId);
        Ensure(stable);
        db.StableWeightReadings.Add(stable.Value);
        var heartbeat = DeviceHeartbeat.Create(
            DevelopmentSeedIds.DeterministicGuid(212, 1),
            DevelopmentSeedIds.TenantId,
            device.Value.Id,
            DemoNowUtc.AddSeconds(-21),
            DemoNowUtc.AddSeconds(-20),
            "loadix-edge-simulator/1.0",
            "Healthy",
            "{\"queueDepth\":0,\"source\":\"synthetic-demo\"}");
        Ensure(heartbeat);
        db.DeviceHeartbeats.Add(heartbeat.Value);
    }

    private static void Ensure(Result result)
    {
        if (result.IsFailure) throw new InvalidOperationException(result.Error!.Description);
    }

    private static void Ensure<T>(Result<T> result)
    {
        if (result.IsFailure) throw new InvalidOperationException(result.Error!.Description);
    }
}

public static class DemoPdf
{
    public static byte[] Create(string title, string body)
    {
        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var stream = $"BT /F1 18 Tf 72 740 Td ({Escape(title)}) Tj 0 -34 Td /F1 11 Tf ({Escape(body)}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(offset.ToString("0000000000", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" 00000 n \n");
        }
        builder.Append("trailer << /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
