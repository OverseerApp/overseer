using System.Linq.Expressions;

namespace Overseer.Server.Data;

public interface IRepository<T>
  where T : class
{
  IReadOnlyList<T> GetAll();

  IReadOnlyList<T> Filter(Expression<Func<T, bool>> predicate);

  T GetById(int id);

  T Get(Expression<Func<T, bool>> predicate);

  void Create(T entity);

  void Update(T entity);

  void Update(IEnumerable<T> entities);

  void Delete(int id);

  void Delete(Expression<Func<T, bool>> predicate);

  void DeleteAll();

  bool Exist(Expression<Func<T, bool>> predicate);

  int Count();

  int Count(Expression<Func<T, bool>> predicate);
}
