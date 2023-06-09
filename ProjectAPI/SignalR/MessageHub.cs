using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ProjectAPI.DTO;
using ProjectAPI.Extensions;
using ProjectAPI.Interfaces;
using ProjectAPI.Models;

namespace ProjectAPI.SignalR
{
    [Authorize]
    public class MessageHub : Hub
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public MessageHub(IMessageRepository messageRepository, IUserRepository userRepository, IMapper mapper)
        {
            _mapper = mapper;
            _messageRepository = messageRepository;
            _userRepository = userRepository;
        }

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var otherUser = httpContext.Request.Query["user"];
            var groupName = GetGroupName(Context.User.GetUserName(), otherUser);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            var messages = await _messageRepository
                .GetMessageThread(Context.User.GetUserName(), otherUser);

            await Clients.Group(groupName).SendAsync("ReceiveMessageThread", messages);
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            return base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(CreateMessageDto createMessageDto)
        {
            var username = Context.User.GetUserName();

            if (username == createMessageDto.RecipientUserName.ToLower())
                throw new HubException("You cannot send a message to yourself");

            var sender = await _userRepository.GetUserByUserNameAsync(username);
            var recipient = await _userRepository.GetUserByUserNameAsync(createMessageDto.RecipientUserName);

            if (recipient == null) throw new HubException("Not found user");

            var message = new Message
            {
                Sender = sender,
                Recipient = recipient,
                SenderUserName = sender.UserName,
                RecipientUserName = recipient.UserName,
                Content = createMessageDto.Content
            };

            _messageRepository.AddMessage(message);

            if (await _messageRepository.SaveAllAsync())
            {
                var group = GetGroupName(sender.UserName, recipient.UserName);
                await Clients.Group(group).SendAsync("NewMessage", _mapper.Map<MessageDto>(message));
            }
        }




        private string GetGroupName(string caller, string other)
        {
            var stringCompare = string.CompareOrdinal(caller, other) < 0;
            return stringCompare ? $"{caller}-{other}" : $"{other}-{caller}";
        }
    }
}