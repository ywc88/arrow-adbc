/*
* Licensed to the Apache Software Foundation (ASF) under one or more
* contributor license agreements.  See the NOTICE file distributed with
* this work for additional information regarding copyright ownership.
* The ASF licenses this file to You under the Apache License, Version 2.0
* (the "License"); you may not use this file except in compliance with
* the License.  You may obtain a copy of the License at
*
*    http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Apache.Arrow.Adbc.Client;
using Apache.Arrow.Adbc.Drivers.BigQuery;
using Apache.Arrow.Adbc.Tests.Xunit;
using Xunit;

namespace Apache.Arrow.Adbc.Tests.Drivers.BigQuery
{
    /// <summary>
    /// Class for testing the ADBC Client using the BigQuery ADBC driver.
    /// </summary>
    /// /// <remarks>
    /// Tests are ordered to ensure data is created for the other
    /// queries to run.
    /// </remarks>
    [TestCaseOrderer("Apache.Arrow.Adbc.Tests.Xunit.TestOrderer", "Apache.Arrow.Adbc.Tests")]
    public class ClientTests
    {
        public ClientTests()
        {
            Skip.IfNot(Utils.CanExecuteTestConfig(BigQueryTestingUtils.BIGQUERY_TEST_CONFIG_VARIABLE));
        }

        /// <summary>
        /// Validates if the client execute updates.
        /// </summary>
        [SkippableFact, Order(1)]
        public void CanClientExecuteUpdate()
        {
            BigQueryTestConfiguration testConfiguration = Utils.LoadTestConfiguration<BigQueryTestConfiguration>(BigQueryTestingUtils.BIGQUERY_TEST_CONFIG_VARIABLE);

            using (Adbc.Client.AdbcConnection adbcConnection = GetAdbcConnection(testConfiguration))
            {
                adbcConnection.Open();

                string[] queries = BigQueryTestingUtils.GetQueries(testConfiguration);

                List<int> expectedResults = new List<int>() { -1, 1, 1, 1 };

                for (int i = 0; i < queries.Length; i++)
                {
                    string query = queries[i];
                    AdbcCommand adbcCommand = adbcConnection.CreateCommand();
                    adbcCommand.CommandText = query;

                    int rows = adbcCommand.ExecuteNonQuery();

                    Assert.Equal(expectedResults[i], rows);
                }
            }
        }

        /// <summary>
        /// Validates if the client can get the schema.
        /// </summary>
        [SkippableFact, Order(2)]
        public void CanClientGetSchema()
        {
            BigQueryTestConfiguration testConfiguration = Utils.LoadTestConfiguration<BigQueryTestConfiguration>(BigQueryTestingUtils.BIGQUERY_TEST_CONFIG_VARIABLE);

            using (Adbc.Client.AdbcConnection adbcConnection = GetAdbcConnection(testConfiguration))
            {
                AdbcCommand adbcCommand = new AdbcCommand(testConfiguration.Query, adbcConnection);

                adbcConnection.Open();

                AdbcDataReader reader = adbcCommand.ExecuteReader(CommandBehavior.SchemaOnly);

                DataTable table = reader.GetSchemaTable();

                // there is one row per field
                Assert.Equal(testConfiguration.Metadata.ExpectedColumnCount, table.Rows.Count);
            }
        }

        /// <summary>
        /// Validates if the client can connect to a live server and
        /// parse the results.
        /// </summary>
        [SkippableFact, Order(3)]
        public void CanClientExecuteQuery()
        {
            BigQueryTestConfiguration testConfiguration = Utils.LoadTestConfiguration<BigQueryTestConfiguration>(BigQueryTestingUtils.BIGQUERY_TEST_CONFIG_VARIABLE);

            long count = 0;

            using (Adbc.Client.AdbcConnection adbcConnection = GetAdbcConnection(testConfiguration))
            {
                AdbcCommand adbcCommand = new AdbcCommand(testConfiguration.Query, adbcConnection);

                adbcConnection.Open();

                AdbcDataReader reader = adbcCommand.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        count++;

                        for(int i=0;i<reader.FieldCount;i++)
                        {
                            object value = reader.GetValue(i);

                            if (value == null)
                                value = "(null)";

                            Console.WriteLine($"{reader.GetName(i)}: {value}");
                        }
                    }
                }
                finally { reader.Close(); }
            }

            Assert.Equal(testConfiguration.ExpectedResultsCount, count);
        }

        /// <summary>
        /// Validates if the client is retrieving and converting values
        /// to the expected types.
        /// </summary>
        [SkippableFact, Order(4)]
        public void VerifyTypesAndValues()
        {
            BigQueryTestConfiguration testConfiguration = Utils.LoadTestConfiguration<BigQueryTestConfiguration>(BigQueryTestingUtils.BIGQUERY_TEST_CONFIG_VARIABLE);

            Adbc.Client.AdbcConnection dbConnection = GetAdbcConnection(testConfiguration);

            dbConnection.Open();
            DbCommand dbCommand = dbConnection.CreateCommand();
            dbCommand.CommandText = testConfiguration.Query;

            DbDataReader reader = dbCommand.ExecuteReader(CommandBehavior.Default);

            if (reader.Read())
            {
                var column_schema = reader.GetColumnSchema();
                DataTable dataTable = reader.GetSchemaTable();

                List<ColumnNetTypeArrowTypeValue> expectedValues = SampleData.GetSampleData();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    object value = reader.GetValue(i);
                    ColumnNetTypeArrowTypeValue ctv = expectedValues[i];

                    Tests.ClientTests.AssertTypeAndValue(ctv, value, reader, column_schema, dataTable);
                }
            }
        }

        private Adbc.Client.AdbcConnection GetAdbcConnection(BigQueryTestConfiguration testConfiguration)
        {
            return new Adbc.Client.AdbcConnection(
                new BigQueryDriver(),
                BigQueryTestingUtils.GetBigQueryParameters(testConfiguration),
                new Dictionary<string,string>()
            );
        }
    }
}
