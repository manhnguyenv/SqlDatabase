﻿using System;
using Moq;
using NUnit.Framework;
using SqlDatabase.Scripts;

namespace SqlDatabase.Commands
{
    [TestFixture]
    public class DatabaseExecuteCommandTest
    {
        private DatabaseExecuteCommand _sut;
        private Mock<IDatabase> _database;
        private Mock<ICreateScriptSequence> _scriptSequence;

        [SetUp]
        public void BeforeEachTest()
        {
            _database = new Mock<IDatabase>(MockBehavior.Strict);
            _database.SetupGet(d => d.ConnectionString).Returns(@"Data Source=unknownServer;Initial Catalog=unknownDatabase");
            _database.Setup(d => d.GetServerVersion()).Returns("sql server 1.0");

            _scriptSequence = new Mock<ICreateScriptSequence>(MockBehavior.Strict);

            var log = new Mock<ILogger>(MockBehavior.Strict);
            log.Setup(l => l.Indent()).Returns((IDisposable)null);
            log
                .Setup(l => l.Info(It.IsAny<string>()))
                .Callback<string>(m =>
                {
                    Console.WriteLine("Info: {0}", m);
                });

            _sut = new DatabaseExecuteCommand
            {
                Database = _database.Object,
                Log = log.Object,
                ScriptSequence = _scriptSequence.Object
            };
        }

        [Test]
        public void ExecuteOneScript()
        {
            var script1 = new Mock<IScript>(MockBehavior.Strict);
            script1.SetupGet(s => s.DisplayName).Returns("step 1");

            var script2 = new Mock<IScript>(MockBehavior.Strict);
            script2.SetupGet(s => s.DisplayName).Returns("step 2");

            _database
                .Setup(d => d.Execute(script1.Object))
                .Callback(() => _database.Setup(d => d.Execute(script2.Object)));

            _scriptSequence.Setup(s => s.BuildSequence()).Returns(new[] { script1.Object, script2.Object });

            _sut.Execute();

            _database.VerifyAll();
            script1.VerifyAll();
            script2.VerifyAll();
        }
    }
}
