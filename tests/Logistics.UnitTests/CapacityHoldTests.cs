using System;
using FluentAssertions;
using Logistics.Domain.Aggregates;
using Xunit;

namespace Logistics.UnitTests
{
    public class CapacityHoldTests
    {
        [Fact]
        public void New_hold_is_active_and_expires_at_created_plus_ttl()
        {
            var now = DateTime.UtcNow;
            var h = new CapacityHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, now, TimeSpan.FromMinutes(5));

            h.Status.Should().Be(HoldStatus.Active);
            h.ExpiresAt.Should().Be(now.AddMinutes(5));
            h.IsExpired(now.AddMinutes(6)).Should().BeTrue();
        }

        [Fact]
        public void Confirm_before_expiry_marks_confirmed()
        {
            var now = DateTime.UtcNow;
            var h = new CapacityHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, now, TimeSpan.FromMinutes(5));

            h.Confirm(now.AddMinutes(1));

            h.Status.Should().Be(HoldStatus.Confirmed);
        }

        [Fact]
        public void Cannot_confirm_after_expiry()
        {
            var now = DateTime.UtcNow;
            var h = new CapacityHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, now, TimeSpan.FromMinutes(1));

            Action expire = () => h.Expire(now.AddMinutes(2));
            expire();

            Action confirm = () => h.Confirm(now.AddMinutes(3));
            confirm.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Expire_is_idempotent()
        {
            var now = DateTime.UtcNow;
            var h = new CapacityHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, now, TimeSpan.FromMinutes(1));

            h.Expire(now.AddMinutes(2));
            h.Status.Should().Be(HoldStatus.Expired);
            // second call should not throw
            Action second = () => h.Expire(now.AddMinutes(3));
            second.Should().NotThrow();
        }

        [Fact]
        public void Confirmed_hold_cannot_expire_as_a_second_terminal_effect()
        {
            var now = DateTime.UtcNow;
            var h = new CapacityHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, now, TimeSpan.FromMinutes(5));

            h.Confirm(now.AddMinutes(1));

            Action expire = () => h.Expire(now.AddMinutes(6));
            expire.Should().NotThrow();
            h.Status.Should().Be(HoldStatus.Confirmed);
        }
    }
}
