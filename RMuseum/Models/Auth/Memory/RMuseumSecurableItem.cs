using RSecurityBackend.Models.Auth.Memory;
using System.Collections.Generic;

namespace RMuseum.Models.Auth.Memory
{
    /// <summary>
    /// specific forms and permission
    /// </summary>
    public class RMuseumSecurableItem : SecurableItem
    {
        /// <summary>
        /// artifact
        /// </summary>
        public const string ArtifactEntityShortName = "artifact";

        /// <summary>
        /// tag
        /// </summary>
        public const string TagEntityShortName = "tag";

        /// <summary>
        /// note
        /// </summary>
        public const string NoteEntityShortName = "note";

        ///<summary>
        /// view drafts
        /// </summary>
        public const string ViewDraftOperationShortName = "viewdraft";

        ///<summary>
        /// edittag
        /// </summary>
        public const string EditTagValueOperationShortName = "edittag";

        ///<summary>
        /// awaiting
        /// </summary>
        public const string ToAwaitingStatusOperationShortName = "awaiting";

        ///<summary>
        /// publish
        /// </summary>
        public const string PublishOperationShortName = "publish";

        ///<summary>
        /// import
        /// </summary>
        public const string ImportOperationShortName = "import";

        ///<summary>
        /// moderate
        /// </summary>
        public const string ModerateOperationShortName = "moderate";

        ///<summary>
        /// review suggested ganjoor links
        /// </summary>
        public const string ReviewGanjoorLinksOperationShortName = "ganjoor";

        /// <summary>
        /// audio narrations
        /// </summary>
        public const string AudioRecitationEntityShortName = "recitation";

        /// <summary>
        /// ganjoor contents
        /// </summary>
        public const string GanjoorEntityShortName = "ganjoor";

        /// <summary>
        /// FAQ contents
        /// </summary>
        public const string FAQEntityShortName = "faq";

        ///<summary>
        /// reorder
        /// </summary>
        public const string ReOrderOperationShortName = "reorder";

        /// <summary>
        /// review suggested songs
        /// </summary>
        public const string ReviewSongs = "songrevu";

        /// <summary>
        /// add song from any source
        /// </summary>
        public const string AddSongs = "songadd";

        /// <summary>
        /// manage footer bannaers
        /// </summary>
        public const string Banners = "banners";

        /// <summary>
        /// donations
        /// </summary>
        public const string Donations = "donations";

        /// <summary>
        /// translations
        /// </summary>
        public const string Translations = "translations";

        /// <summary>
        /// photos
        /// </summary>
        public const string ModeratePoetPhotos = "photos";

        /// <summary>
        /// pdf
        /// </summary>
        public const string PDFLibraryEntityShortName = "pdf";

        /// <summary>
        /// user reports against books - deliberately its own entity rather than folded into
        /// PDFLibraryEntityShortName, since a reviewer here doesn't necessarily need (and
        /// shouldn't automatically get) PDF delete permission, or vice versa
        /// </summary>
        public const string PDFBookReportEntityShortName = "pdfreport";

        /// <summary>
        /// moderating other users' page comments (deleting a comment that isn't your own) -
        /// deliberately separate from PDFBookReportEntityShortName, since reviewing book
        /// reports and moderating comments are different responsibilities. A user can always
        /// delete their own comment regardless of this permission.
        /// </summary>
        public const string PDFPageCommentEntityShortName = "pdfcomment";

        /// <summary>
        /// reviewing user reports against page comments (as opposed to PDFPageCommentEntityShortName,
        /// which is unilateral comment moderation with no report attached) - deliberately its own
        /// entity, same reasoning as PDFBookReportEntityShortName vs PDFLibraryEntityShortName: a
        /// reviewer here doesn't necessarily need (and shouldn't automatically get) blanket
        /// comment-delete power, or vice versa. Resolving a report as approved does delete that one
        /// specific reported comment as part of the resolution, but that's scoped to the report
        /// being reviewed, not a general grant of PDFPageCommentEntityShortName's own moderate
        /// operation.
        /// </summary>
        public const string PDFPageCommentReportEntityShortName = "pdfcommentreport";

        /// <summary>
        /// moderating other users' book reviews (deleting a review that isn't your own) -
        /// same reasoning as PDFPageCommentEntityShortName: a user can always delete their
        /// own review regardless of this permission. Review-report reviewing, when that's
        /// built, will be its own separate entity too, same split as
        /// PDFPageCommentReportEntityShortName vs PDFPageCommentEntityShortName.
        /// </summary>
        public const string PDFBookReviewEntityShortName = "pdfbookreview";

        /// <summary>
        /// reviewing user reports against book reviews - its own separate entity from
        /// PDFBookReviewEntityShortName, same split as PDFPageCommentReportEntityShortName vs
        /// PDFPageCommentEntityShortName and for the same reason: a reviewer here doesn't
        /// necessarily need (and shouldn't automatically get) blanket review-delete power, or
        /// vice versa.
        /// </summary>
        public const string PDFBookReviewReportEntityShortName = "pdfbookreviewreport";

        /// <summary>
        /// ftp
        /// </summary>
        public const string QueuedFTPUploadShortName = "ftp";

        /// <summary>
        /// list of forms and their permissions
        /// </summary>
        public new static SecurableItem[] Items
        {
            get
            {
                List<SecurableItem> lst = new List<SecurableItem>(SecurableItem.Items);
                lst.AddRange(
                new SecurableItem[]
                {
                    new SecurableItem()
                    {
                        ShortName = ArtifactEntityShortName,
                        Description = "اشیاء گنجینه",
                        Operations = new SecurableItemOperation[]
                        {                            
                            new SecurableItemOperation(AddOperationShortName, "ایجاد", false),
                            new SecurableItemOperation(ImportOperationShortName, "ورود اطلاعات از منابع خارجی", false),
                            new SecurableItemOperation(ModifyOperationShortName, "اصلاح", false),
                            new SecurableItemOperation(DeleteOperationShortName, "حذف", false),
                            new SecurableItemOperation(ViewDraftOperationShortName, "مشاهدهٔ پیش‌نویس‌ها", false),
                            new SecurableItemOperation(EditTagValueOperationShortName, "اصلاح مقدار ویژگی", false),
                            new SecurableItemOperation(ToAwaitingStatusOperationShortName, "درخواست بازبینی", false),
                            new SecurableItemOperation(PublishOperationShortName, "انتشار", false),
                            new SecurableItemOperation(ReviewGanjoorLinksOperationShortName, "بررسی شعرهای پیشنهادی گنجور", false),
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = TagEntityShortName,
                        Description = "انواع ویژگیها",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(AddOperationShortName, "ایجاد", false),
                            new SecurableItemOperation(ModifyOperationShortName, "اصلاح", false),
                            new SecurableItemOperation(DeleteOperationShortName, "حذف", false)
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = NoteEntityShortName,
                        Description = "یادداشتها",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(ModerateOperationShortName, "بررسی", false)
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = AudioRecitationEntityShortName,
                        Description = "خوانش‌ها",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(PublishOperationShortName, "انتشار خوانش خود", false),
                            new SecurableItemOperation(ModerateOperationShortName, "بررسی خوانش کاربران دیگر", false),
                            new SecurableItemOperation(ReOrderOperationShortName, "تغییر ترتیب خوانش‌ها", false),
                            new SecurableItemOperation(ImportOperationShortName, "ورود اطلاعات از منابع خارجی", false),
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = GanjoorEntityShortName,
                        Description = "محتوای گنجور",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(ReviewSongs, "بازبینی آهنگ‌های پیشنهادی", false),
                            new SecurableItemOperation(AddSongs, "افزودن آهنگ از هر منبع", false),
                            new SecurableItemOperation(ImportOperationShortName, "ورود اطلاعات از منابع خارجی", false),
                            new SecurableItemOperation(ModerateOperationShortName, "مدیریت حاشیه‌ها", false),
                            new SecurableItemOperation(ModifyOperationShortName, "ویرایش محتوا", false),
                            new SecurableItemOperation(Banners, "مدیریت آگاهی‌ها", false),
                            new SecurableItemOperation(Donations, "مدیریت کمکهای مالی", false),
                            new SecurableItemOperation(Translations, "ترجمه", false),
                            new SecurableItemOperation(ModeratePoetPhotos, "مدیریت تصاویر سخنوران", false),
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = FAQEntityShortName,
                        Description = "پرسش‌های متداول",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(ModerateOperationShortName, "مدیریت", false),
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = PDFLibraryEntityShortName,
                        Description = "نسکبان",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(AddOperationShortName, "ایجاد", false),
                            new SecurableItemOperation(ImportOperationShortName, "ورود اطلاعات از منابع خارجی", false),
                            new SecurableItemOperation(ModifyOperationShortName, "ویرایش محتوا", false),
                            new SecurableItemOperation(DeleteOperationShortName, "حذف", false),
                            new SecurableItemOperation(ViewDraftOperationShortName, "مشاهدهٔ پیش‌نویس‌ها", false),
                            new SecurableItemOperation(EditTagValueOperationShortName, "اصلاح مقدار ویژگی", false),
                            new SecurableItemOperation(ToAwaitingStatusOperationShortName, "درخواست بازبینی", false),
                            new SecurableItemOperation(PublishOperationShortName, "انتشار", false),
                            new SecurableItemOperation(ReviewGanjoorLinksOperationShortName, "بررسی شعرهای پیشنهادی گنجور", false),
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = PDFBookReportEntityShortName,
                        Description = "گزارش‌های کاربران دربارهٔ کتاب‌ها",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(ModerateOperationShortName, "بررسی و پاسخ به گزارش‌ها", false),
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = PDFPageCommentEntityShortName,
                        Description = "دیدگاه‌های کاربران روی صفحات کتاب‌ها",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(ModerateOperationShortName, "حذف دیدگاه دیگر کاربران", false),
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = PDFPageCommentReportEntityShortName,
                        Description = "گزارش‌های کاربران دربارهٔ دیدگاه‌ها",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(ModerateOperationShortName, "بررسی و پاسخ به گزارش‌های دیدگاه", false),
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = PDFBookReviewEntityShortName,
                        Description = "نقدهای کاربران دربارهٔ کتاب‌ها",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(ModerateOperationShortName, "حذف نقد دیگر کاربران", false),
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = PDFBookReviewReportEntityShortName,
                        Description = "گزارش‌های کاربران دربارهٔ نقدها",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(ModerateOperationShortName, "بررسی و پاسخ به گزارش‌های نقد", false),
                        }
                    },
                    new SecurableItem()
                    {
                        ShortName = QueuedFTPUploadShortName,
                        Description = "بارگذاری به FTP خارجی",
                        Operations = new SecurableItemOperation[]
                        {
                            new SecurableItemOperation(ModerateOperationShortName, "مدیریت", false),
                        }
                    },



                });
                return lst.ToArray();
            }
        }
    }
}
