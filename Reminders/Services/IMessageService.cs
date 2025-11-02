using HereAndNowService.Models;

namespace HereAndNowService.Services;

public interface IMessageService
{
    Message GetPublicMessage();
    Message GetProtectedMessage();
    Message GetAdminMessage();
}
