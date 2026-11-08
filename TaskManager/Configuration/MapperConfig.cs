using AutoMapper;
using TaskManager.DTO;
using TaskManager.Models;

namespace TaskManager.Configuration
{
    public class MapperConfig : Profile
    {
        public MapperConfig()
        {
            CreateMap<User, UserReadOnlyDTO>()
                 .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.Name));

            CreateMap<User, UserSummaryDTO>();

            CreateMap<ProjectTask, TaskReadOnlyDTO>()
                .ForMember(dest => dest.ProjectUuid, opt => opt.MapFrom(src => src.Project.Uuid))
                .ForMember(dest => dest.Members, opt => opt.MapFrom(src => src.Users))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.User));

            CreateMap<ProjectTask, TaskSummaryDTO>()
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.User));

            CreateMap<Project, ProjectReadOnlyDTO>()
                .ForMember(dest => dest.Members, opt => opt.MapFrom(src => src.Users))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.User))
                .ForMember(dest => dest.TaskCount, opt => opt.MapFrom(src => src.ProjectTasks.Count(t => !t.IsDeleted)));

            CreateMap<Project, ProjectDetailsDTO>()
                .IncludeBase<Project, ProjectReadOnlyDTO>()
                .ForMember(dest => dest.Tasks, opt => opt.MapFrom(src => src.ProjectTasks));
        }
       
    }
}
