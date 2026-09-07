using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderContactConfiguration : IEntityTypeConfiguration<OrderContact>
    {
        public void Configure(EntityTypeBuilder<OrderContact> builder)
        {
            builder.ToTable("OrderContacts");
            builder.HasKey(contact => contact.Id);
            builder.Property(contact => contact.Id).ValueGeneratedNever();
            builder.Property(contact => contact.ContactName).HasMaxLength(256);

            builder.HasMany(contact => contact.ContactPoints).WithOne().HasForeignKey(point => point.OrderContactId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(contact => contact.ContactPoints).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
