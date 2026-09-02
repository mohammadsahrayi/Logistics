using System;
using FluentAssertions;
using Logistics.Domain.Aggregates;
using Xunit;

namespace Logistics.UnitTests
{
    public class VoyageCapacityTests
    {
        [Fact]
        public void TryReserve_reduces_available_and_sets_held()
        {
            var voyageId = Guid.NewGuid();
            var v = new VoyageCapacity(voyageId, totalCapacity: 10);

            var ok = v.TryReserve(3);

            ok.Should().BeTrue();
            v.HeldCapacity.Should().Be(3);
            v.AvailableCapacity.Should().Be(7);
        }

        [Fact]
        public void TryReserve_fails_when_insufficient()
        {
            var voyageId = Guid.NewGuid();
            var v = new VoyageCapacity(voyageId, totalCapacity: 2);

            var ok = v.TryReserve(3);

            ok.Should().BeFalse();
            v.HeldCapacity.Should().Be(0);
            v.AvailableCapacity.Should().Be(2);
        }

        [Fact]
        public void ConfirmReserved_moves_held_to_confirmed()
        {
            var v = new VoyageCapacity(Guid.NewGuid(), 5);
            v.TryReserve(2);

            v.ConfirmReserved(2);

            v.HeldCapacity.Should().Be(0);
            v.ConfirmedCapacity.Should().Be(2);
            v.AvailableCapacity.Should().Be(3);
        }

        [Fact]
        public void ReleaseReserved_releases_held()
        {
            var v = new VoyageCapacity(Guid.NewGuid(), 5);
            v.TryReserve(2);

            v.ReleaseReserved(2);

            v.HeldCapacity.Should().Be(0);
            v.AvailableCapacity.Should().Be(5);
        }
    }
}
