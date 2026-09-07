using AeroTech.Framework.Core.ServiceContracts;
using IdGen;

namespace AeroTech.Framework.Infrastructure.Services
{
    public sealed class SnowflakeIdGenerator : IIdGenerator
    {
        private readonly IdGenerator _generator;

        public SnowflakeIdGenerator(int generatorId) => _generator = new IdGenerator(generatorId);

        public long NewId() => _generator.CreateId();
    }
}
