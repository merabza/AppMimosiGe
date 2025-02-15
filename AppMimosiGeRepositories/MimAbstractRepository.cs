//Created by ProjectAbstractRepositoryClassCreator at 2/15/2025 11:07:44 AM

using AppMimosiGeDb;
using RepositoriesDom;

namespace AppMimosiGeRepositories;

public sealed class MimAbstractRepository(AppMimosiGeDbContext ctx) : AbstractRepository(ctx);