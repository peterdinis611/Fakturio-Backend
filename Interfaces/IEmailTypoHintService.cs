using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IEmailTypoHintService
{
    ServiceResult<EmailTypoHintResponse> Suggest(string email);
}