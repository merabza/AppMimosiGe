//Created by ProjectAbstractRepositoryClassCreator at 2/15/2025 11:07:44 AM

using MimosiGeDb;
using RepositoriesDom;

namespace AppMimosiGeRepositories;

public sealed class MimAbstractRepository(MimosiGeDbContext ctx) : AbstractRepository(ctx);