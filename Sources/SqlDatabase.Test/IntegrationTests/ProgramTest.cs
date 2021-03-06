﻿using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Dapper;
using Moq;
using NUnit.Framework;
using SqlDatabase.Configuration;
using SqlDatabase.Scripts;
using SqlDatabase.TestApi;
using ConfigurationManager = System.Configuration.ConfigurationManager;

namespace SqlDatabase.IntegrationTests
{
    [TestFixture]
    public class ProgramTest
    {
        private readonly string _connectionString = new SqlConnectionStringBuilder(Query.ConnectionString) { InitialCatalog = "SqlDatabaseIT" }.ToString();

        private Mock<ILogger> _log;
        private string _scriptsLocation;
        private AppConfiguration _configuration;

        [SetUp]
        public void BeforeEachTest()
        {
            _scriptsLocation = ConfigurationManager.AppSettings["IntegrationTestsScriptsLocation"];
            if (!Path.IsPathRooted(_scriptsLocation))
            {
                _scriptsLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _scriptsLocation);
            }

            _configuration = new AppConfiguration();

            _log = new Mock<ILogger>(MockBehavior.Strict);
            _log
                .Setup(l => l.Error(It.IsAny<string>()))
                .Callback<string>(m =>
                {
                    Console.WriteLine("Error: {0}", m);
                });
            _log
                .Setup(l => l.Info(It.IsAny<string>()))
                .Callback<string>(m =>
                {
                    Console.WriteLine("Info: {0}", m);
                });
        }

        [Test]
        [Order(1)]

        public void CreateDatabase()
        {
            var args = new CommandLineBuilder()
                .SetCommand(Command.Create)
                .SetConnection(_connectionString)
                .SetScripts(Path.Combine(_scriptsLocation, "new"))
                .SetVariable("JohnCity", "London")
                .SetVariable("MariaCity", "Paris")
                .BuildArray();

            Assert.AreEqual(0, Program.Main(args));

            const string Sql = @"
SELECT Person.Id, Person.Name, PersonAddress.City
FROM demo.Person Person
     INNER JOIN demo.PersonAddress PersonAddress ON (PersonAddress.PersonId = Person.Id)
ORDER BY Person.Id";

            var db = new Database
            {
                ConnectionString = _connectionString,
                Configuration = _configuration
            };
            Assert.AreEqual(new Version("1.2"), db.GetCurrentVersion());

            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                var rows = c.Query(Sql).ToList();
                Assert.AreEqual(2, rows.Count);

                Assert.AreEqual(1, rows[0].Id);
                Assert.AreEqual("John", rows[0].Name);
                Assert.AreEqual("London", rows[0].City);

                Assert.AreEqual(2, rows[1].Id);
                Assert.AreEqual("Maria", rows[1].Name);
                Assert.AreEqual("Paris", rows[1].City);
            }
        }

        [Test]
        [Order(2)]
        public void UpgradeDatabase()
        {
            var args = new CommandLineBuilder()
                .SetCommand(Command.Upgrade)
                .SetConnection(_connectionString)
                .SetScripts(Path.Combine(_scriptsLocation, "upgrade"))
                .SetVariable("JohnSecondName", "Smitt")
                .SetVariable("MariaSecondName", "X")
                .BuildArray();

            Assert.AreEqual(0, Program.Main(args));

            const string Sql = @"
SELECT Person.Id, Person.SecondName
FROM demo.Person Person
ORDER BY Person.Id";

            var db = new Database
            {
                ConnectionString = _connectionString,
                Configuration = _configuration
            };
            Assert.AreEqual(new Version("2.1"), db.GetCurrentVersion());

            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                var rows = c.Query(Sql).ToList();
                Assert.AreEqual(2, rows.Count);

                Assert.AreEqual(1, rows[0].Id);
                Assert.AreEqual("Smitt", rows[0].SecondName);

                Assert.AreEqual(2, rows[1].Id);
                Assert.AreEqual("X", rows[1].SecondName);
            }
        }

        [Test]
        [Order(3)]
        public void ExecuteScript()
        {
            var args = new CommandLineBuilder()
                .SetCommand(Command.Execute)
                .SetConnection(_connectionString)
                .SetScripts(Path.Combine(_scriptsLocation, "execute", "drop.database.sql"))
                .BuildArray();

            Assert.AreEqual(0, Program.Main(args));

            var sql = "SELECT DB_ID('{0}')".FormatWith(new SqlConnectionStringBuilder(_connectionString).InitialCatalog);

            using (var c = new SqlConnection(Query.ConnectionString))
            {
                c.Open();

                var test = c.ExecuteScalar(sql);
                Assert.IsNull(test);
            }
        }
    }
}
