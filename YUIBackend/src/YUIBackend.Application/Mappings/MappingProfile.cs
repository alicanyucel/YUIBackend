using AutoMapper;
using MyProject.Application.Features.Products.Dtos;
using MyProject.Domain.Entities;

namespace MyProject.Application.Mappings;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Product, ProductDto>().ReverseMap();
    }
}
