using STailor.Core.Application.Commands;
using STailor.Core.Application.Migration;

namespace STailor.Core.Application.Abstractions.Services;

public interface ILegacyMigrationMapper
{
    CreateCustomerCommand MapCustomer(LegacyCustomerRecord record);

    CreateOrderCommand MapOrder(LegacyOrderRecord record, Guid mappedCustomerId);
}
