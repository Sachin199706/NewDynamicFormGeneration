using AutoMapper;
using NewDynamicFormGenAPI.Models.DTOs.Submissions;
using NewDynamicFormGenAPI.Models.Entities;
using System.Text.Json;

namespace NewDynamicFormGenAPI.Models.Helper;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Add your mappings here, for example:
        // CreateMap<Source, Destination();
        CreateMap<SubmitFormDto, FormSubmission>()
    .ForMember(dest => dest.SubmittedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
    .ForMember(dest => dest.JsonData, opt => opt.MapFrom(src => JsonSerializer.Serialize(src.Values, (JsonSerializerOptions?)null)))
    .ForMember(dest => dest.SubmissionCode, opt => opt.MapFrom(src => "PENDING"));
    }
}
