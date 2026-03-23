using AutoMapper;
using BlogAPI.Models;
using BlogAPI.Dtos;

namespace BlogAPI.Profiles
{
    // BlogProfile defines AutoMapper mappings between domain models (entities)
    // and DTOs used by the API. This keeps mapping logic in a single place
    public class BlogProfile : Profile
    {
        public BlogProfile()
        {
            // Map BlogPost entity to PostResponse DTO.
            // UserName and CategoryName are taken from navigation properties
            CreateMap<BlogPost, PostResponse>()
                .ForMember(dest => dest.UserName,
                    opt => opt.MapFrom(src => src.User.UserName))
                .ForMember(dest => dest.CategoryName,
                    opt => opt.MapFrom(src => src.Category.Name));

            // Map Comment entity to CommentResponse DTO.
            // UserName is taken from the related User entity
            CreateMap<Comment, CommentResponse>()
            .ForMember(dest => dest.UserName,
                opt => opt.MapFrom(src => src.User.UserName));

            // Map User entity to UserResponse DTO.
            // AutoMapper maps properties with the same names automatically
            CreateMap<User, UserResponse>();

        }
    }
}
