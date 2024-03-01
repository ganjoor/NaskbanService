using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFUserTracking
{
    /// <summary>
    /// Visit record
    /// </summary>
    public class PDFVisitRecord
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// user id
        /// </summary>
        public Guid? RAppUserId { get; set; }

        /// <summary>
        /// user
        /// </summary>
        public virtual RAppUser RAppUser { get; set; }

        /// <summary>
        /// date time
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// PDF Book Id
        /// </summary>
        public int? PDFBookId { get; set; }

        /// <summary>
        /// PDF Page Number
        /// </summary>
        public int? PDFPageNumber { get; set; }

        /// <summary>
        /// search term
        /// </summary>
        public string SearchTerm { get; set; }

        /// <summary>
        /// full text search
        /// </summary>
        public bool IsFullTextSearch { get; set; }

        /// <summary>
        /// page number
        /// </summary>
        public int? PageNumber { get; set; }

        /// <summary>
        /// page size
        /// </summary>
        public int? PageSize { get; set; }
    }
}
