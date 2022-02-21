using AutoMapper;
using MarsOffice.Tvg.TikTok.Abstractions;
using MarsOffice.Tvg.TikTok.Entities;

namespace MarsOffice.Tvg.TikTok.Mappers
{
    public class TikTokAccountMapper : Profile
    {
        public TikTokAccountMapper()
        {
            CreateMap<TikTokAccount, TikTokAccountEntity>().PreserveReferences();
            CreateMap<TikTokAccountEntity, TikTokAccount>().PreserveReferences();
        }
    }
}