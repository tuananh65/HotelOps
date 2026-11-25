using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;
public sealed record AttachChecklistRequest([Required]string RowVersion,[Range(1,long.MaxValue)]long ChecklistTemplateId);
