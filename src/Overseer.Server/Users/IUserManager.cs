using Overseer.Server.Models;

namespace Overseer.Server.Users;

public interface IUserManager
{
  UserDisplay CreateUser(UserDisplay userModel, UserDisplay? createdBy = null);
  void DeleteUser(int userId);
  UserDisplay GetUser(int userId);
  IReadOnlyList<UserDisplay> GetUsers();
  UserDisplay? UpdateUser(UserDisplay user);
  UserDisplay ChangePassword(UserDisplay user, UserDisplay changedBy);
}
