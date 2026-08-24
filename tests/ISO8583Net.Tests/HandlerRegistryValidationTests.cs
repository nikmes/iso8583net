using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ISO8583Tests
{
    public class HandlerRegistryValidationTests
    {
        private sealed class TestHandler : IMessageHandler
        {
            private readonly string[] _mtis;
            public TestHandler(params string[] mtis) => _mtis = mtis;
            public IReadOnlySet<string> SupportedMTIs => new HashSet<string>(_mtis);
            public Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
                => Task.FromResult<ISOMessage?>(null);
        }

        private static ISOMessageFieldsPackager CreateD8FieldsPackager()
        {
            var packager = new ISOMessagePackager(
                NullLogger<HandlerRegistryValidationTests>.Instance, BuiltInDialect.D8);
            return packager.GetISOMessageFieldsPackager();
        }

        [Fact]
        public void ValidateAgainstDialect_DefinedMtiAndCatchAll_Passes()
        {
            var registry = new HandlerRegistry(new IMessageHandler[]
            {
                new TestHandler("1804"),
                new TestHandler("1100"),
                new TestHandler("*"),
            });

            registry.ValidateAgainstDialect(CreateD8FieldsPackager());

            Assert.Equal(2, registry.RegisteredMTIs.Count);
            Assert.True(registry.HasCatchAll);
        }

        [Fact]
        public void ValidateAgainstDialect_UndefinedMti_Throws()
        {
            var registry = new HandlerRegistry(new IMessageHandler[]
            {
                new TestHandler("9999"),
            });

            var ex = Assert.Throws<InvalidOperationException>(
                () => registry.ValidateAgainstDialect(CreateD8FieldsPackager()));

            Assert.Contains("9999", ex.Message);
        }

        [Fact]
        public void ValidateAgainstDialect_FormatErrorMti_Throws()
        {
            var registry = new HandlerRegistry(new IMessageHandler[]
            {
                new TestHandler("9800"),
            });

            var ex = Assert.Throws<InvalidOperationException>(
                () => registry.ValidateAgainstDialect(CreateD8FieldsPackager()));

            Assert.Contains("9800", ex.Message);
        }

        [Fact]
        public void ValidateAgainstDialect_WildcardOtherThanStar_Throws()
        {
            var registry = new HandlerRegistry(new IMessageHandler[]
            {
                new TestHandler("18*"),
            });

            var ex = Assert.Throws<InvalidOperationException>(
                () => registry.ValidateAgainstDialect(CreateD8FieldsPackager()));

            Assert.Contains("18*", ex.Message);
        }

        [Fact]
        public void ValidateAgainstDialect_MultipleErrors_ReportsAll()
        {
            var registry = new HandlerRegistry(new IMessageHandler[]
            {
                new TestHandler("9999", "1234"),
            });

            var ex = Assert.Throws<InvalidOperationException>(
                () => registry.ValidateAgainstDialect(CreateD8FieldsPackager()));

            Assert.Contains("9999", ex.Message);
            Assert.Contains("1234", ex.Message);
        }

        [Fact]
        public void GetHandlers_AfterValidation_IncludesCatchAllForDefinedMti()
        {
            var registry = new HandlerRegistry(new IMessageHandler[]
            {
                new TestHandler("1804"),
                new TestHandler("*"),
            });
            registry.ValidateAgainstDialect(CreateD8FieldsPackager());

            var handlers = registry.GetHandlers("1804");

            Assert.Equal(2, handlers.Count);
        }

        [Fact]
        public void GetHandlers_AfterValidation_ExcludesCatchAllForUndefinedMti()
        {
            var registry = new HandlerRegistry(new IMessageHandler[]
            {
                new TestHandler("*"),
            });
            registry.ValidateAgainstDialect(CreateD8FieldsPackager());

            var handlers = registry.GetHandlers("9999");

            Assert.Empty(handlers);
        }

        [Fact]
        public void GetHandlers_AfterValidation_IncludesCatchAllForEmptyMti()
        {
            var registry = new HandlerRegistry(new IMessageHandler[]
            {
                new TestHandler("*"),
            });
            registry.ValidateAgainstDialect(CreateD8FieldsPackager());

            var handlers = registry.GetHandlers("");

            Assert.Single(handlers);
        }

        [Fact]
        public void GetHandlers_WithoutValidation_StaysPermissive()
        {
            var registry = new HandlerRegistry(new IMessageHandler[]
            {
                new TestHandler("*"),
            });

            var handlers = registry.GetHandlers("9999");

            Assert.Single(handlers);
        }
    }
}
