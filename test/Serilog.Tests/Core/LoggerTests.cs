﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Serilog.Core;
using Serilog.Events;
using Serilog.Tests.Support;

namespace Serilog.Tests.Core
{
    public class LoggerTests
    {
        [Fact]
        public void AnExceptionThrownByAnEnricherIsNotPropagated()
        {
            var thrown = false;

            var l = new LoggerConfiguration()
                .WriteTo.TextWriter(new StringWriter())
                .Enrich.With(new DelegatingEnricher((le, pf) => {
                    thrown = true;
                    throw new Exception("No go, pal."); }))
                .CreateLogger();

            l.Information(Some.String());

            Assert.True(thrown);
        }

        [Fact]
        public void AContextualLoggerAddsTheSourceTypeName()
        {
            var evt = DelegatingSink.GetLogEvent(l => l.ForContext<LoggerTests>()
                                        .Information(Some.String()));

            var lv = evt.Properties[Constants.SourceContextPropertyName].LiteralValue();
            Assert.Equal(typeof(LoggerTests).FullName, lv);
        }

        [Fact]
        public void PropertiesInANestedContextOverrideParentContextValues()
        {
            var name = Some.String();
            var v1 = Some.Int();
            var v2 = Some.Int();
            var evt = DelegatingSink.GetLogEvent(l => l.ForContext(name, v1)
                                        .ForContext(name, v2)
                                        .Write(Some.InformationEvent()));

            var pActual = evt.Properties[name];
            Assert.Equal(v2, pActual.LiteralValue());
        }

        [Fact]
        public void ParametersForAnEmptyTemplateAreIgnored()
        {
            var e = DelegatingSink.GetLogEvent(l => l.Error("message", new object()));
            Assert.Equal("message", e.RenderMessage());
        }

        [Fact]
        public void LoggingLevelSwitchDynamicallyChangesLevel()
        {
            var events = new List<LogEvent>();
            var sink = new DelegatingSink(events.Add);

            var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);

            var log = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Sink(sink)
                .CreateLogger()
                .ForContext<LoggerTests>();

            log.Debug("Suppressed");
            log.Information("Emitted");
            log.Warning("Emitted");

            // Change the level
            levelSwitch.MinimumLevel = LogEventLevel.Error;

            log.Warning("Suppressed");
            log.Error("Emitted");
            log.Fatal("Emitted");

            Assert.Equal(4, events.Count);
            Assert.True(events.All(evt => evt.RenderMessage() == "Emitted"));
        }
    }
}
