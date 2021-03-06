﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Moq;
using NUnit.Framework;
using SqlDatabase.Configuration;
using SqlDatabase.Scripts.AssemblyInternal;
using SqlDatabase.TestApi;

namespace SqlDatabase.Scripts
{
    [TestFixture]
    public partial class AssemblyScriptTest
    {
        private AssemblyScript _sut;
        private Variables _variables;
        private Mock<ILogger> _log;
        private Mock<IDbCommand> _command;

        private IList<string> _logOutput;
        private IList<string> _executedScripts;

        [SetUp]
        public void BeforeEachTest()
        {
            _variables = new Variables();

            _logOutput = new List<string>();
            _log = new Mock<ILogger>(MockBehavior.Strict);
            _log
                .Setup(l => l.Info(It.IsAny<string>()))
                .Callback<string>(m =>
                {
                    Console.WriteLine("Info: {0}", m);
                    _logOutput.Add(m);
                });
            _log
                .Setup(l => l.Error(It.IsAny<string>()))
                .Callback<string>(m =>
                {
                    Console.WriteLine("Error: {0}", m);
                    _logOutput.Add(m);
                });

            _executedScripts = new List<string>();
            _command = new Mock<IDbCommand>(MockBehavior.Strict);
            _command.SetupProperty(c => c.CommandText);
            _command
                .Setup(c => c.ExecuteNonQuery())
                .Callback(() => _executedScripts.Add(_command.Object.CommandText))
                .Returns(0);

            _sut = new AssemblyScript();
            _sut.Configuration = new AssemblyScriptConfiguration();
        }

        [Test]
        public void ExecuteExample()
        {
            _variables.DatabaseName = "dbName";
            _variables.CurrentVersion = "1.0";
            _variables.TargetVersion = "2.0";

            _sut.DisplayName = "2.1_2.2.dll";
            _sut.ReadAssemblyContent = () => File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "2.1_2.2.dll"));

            _sut.Execute(new DbCommandProxy(_command.Object), _variables, _log.Object);

            Assert.IsTrue(_logOutput.Contains("start execution"));

            Assert.IsTrue(_executedScripts.Contains("print 'current database name is dbName'"));
            Assert.IsTrue(_executedScripts.Contains("print 'version from 1.0'"));
            Assert.IsTrue(_executedScripts.Contains("print 'version to 2.0'"));

            Assert.IsTrue(_executedScripts.Contains("create table dbo.DemoTable (Id INT)"));
            Assert.IsTrue(_executedScripts.Contains("print 'drop table DemoTable'"));
            Assert.IsTrue(_executedScripts.Contains("drop table dbo.DemoTable"));

            Assert.IsTrue(_logOutput.Contains("finish execution"));
        }

        [Test]
        public void ValidateScriptDomainAppBase()
        {
            _sut.DisplayName = GetType().Assembly.Location;
            _sut.ReadAssemblyContent = () => File.ReadAllBytes(GetType().Assembly.Location);
            _sut.Configuration = new AssemblyScriptConfiguration(typeof(StepWithSubDomain).Name, nameof(StepWithSubDomain.ShowAppBase));

            _sut.Execute(new DbCommandProxy(_command.Object), _variables, _log.Object);

            Assert.AreEqual(2, _executedScripts.Count);

            var assemblyFileName = _executedScripts[0];
            FileAssert.DoesNotExist(assemblyFileName);
            Assert.AreEqual(Path.GetFileName(assemblyFileName), Path.GetFileName(GetType().Assembly.Location));

            var appBase = _executedScripts[1];
            DirectoryAssert.DoesNotExist(appBase);
            Assert.AreEqual(appBase, Path.GetDirectoryName(assemblyFileName));
        }

        [Test]
        public void ValidateScriptDomainConfiguration()
        {
            _sut.DisplayName = "CreateSubDomain.dll";
            _sut.ReadAssemblyContent = () => File.ReadAllBytes(GetType().Assembly.Location);
            _sut.Configuration = new AssemblyScriptConfiguration(typeof(StepWithSubDomain).Name, nameof(StepWithSubDomain.ShowConfiguration));

            _sut.Execute(new DbCommandProxy(_command.Object), _variables, _log.Object);

            Assert.AreEqual(2, _executedScripts.Count);

            var configurationFile = _executedScripts[0];
            Assert.AreEqual(configurationFile, AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);

            var connectionString = _executedScripts[1];
            Assert.AreEqual(connectionString, Query.ConnectionString);
        }

        [Test]
        public void ValidateScriptDomainCreateSubDomain()
        {
            _sut.DisplayName = GetType().Assembly.Location;
            _sut.ReadAssemblyContent = () => File.ReadAllBytes(GetType().Assembly.Location);
            _sut.Configuration = new AssemblyScriptConfiguration(typeof(StepWithSubDomain).Name, nameof(StepWithSubDomain.Execute));

            _sut.Execute(new DbCommandProxy(_command.Object), _variables, _log.Object);

            Assert.AreEqual(1, _executedScripts.Count);

            Assert.AreEqual("hello", _executedScripts[0]);
        }

        [Test]
        public void FailToResolveExecutor()
        {
            var agent = new DomainAgent
            {
                Logger = _log.Object,
                Assembly = GetType().Assembly
            };

            Assert.Throws<InvalidOperationException>(() => _sut.Execute(agent, _command.Object, _variables));
        }

        [Test]
        public void FailOnExecute()
        {
            var entryPoint = new Mock<IEntryPoint>(MockBehavior.Strict);
            entryPoint
                .Setup(p => p.Execute(_command.Object, It.IsNotNull<IReadOnlyDictionary<string, string>>()))
                .Returns(false);

            var agent = new DomainAgent
            {
                Logger = _log.Object,
                EntryPoint = entryPoint.Object
            };

            Assert.Throws<InvalidOperationException>(() => _sut.Execute(agent, _command.Object, _variables));
        }
    }
}
