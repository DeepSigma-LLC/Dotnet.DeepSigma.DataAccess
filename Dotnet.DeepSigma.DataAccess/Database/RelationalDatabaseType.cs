using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.Database
{
    /// <summary>
    /// Specifies the type of database being used.
    /// </summary>
    public enum RelationalDatabaseType
    {
        /// <summary>
        /// Microsoft SQL Server database.
        /// </summary>
        SQLServer,
        /// <summary>
        /// PostgreSQL database.
        /// </summary>
        Postgres
    }
}
