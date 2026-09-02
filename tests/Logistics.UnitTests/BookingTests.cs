using System;
using FluentAssertions;
using Logistics.Domain.Aggregates;
using Xunit;

namespace Logistics.UnitTests
{
    public class BookingTests
    {
        [Fact]
        public void AttachHold_sets_active_hold()
        {
            var b = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2);
            var holdId = Guid.NewGuid();

            b.AttachHold(holdId);

            b.ActiveHoldId.Should().Be(holdId);
        }

        [Fact]
        public void Confirm_with_matching_hold_marks_confirmed()
        {
            var b = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2);
            var holdId = Guid.NewGuid();
            b.AttachHold(holdId);

            b.Confirm(holdId);

            b.State.Should().Be(BookingState.Confirmed);
        }

        [Fact]
        public void Confirm_without_matching_hold_throws()
        {
            var b = new Booking(Guid.NewGuid(), Guid.NewGuid(), 2);
            var holdId = Guid.NewGuid();

            Action confirm = () => b.Confirm(holdId);

            confirm.Should().Throw<InvalidOperationException>();
        }
    }
}
