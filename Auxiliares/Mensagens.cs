namespace Orcei.Auxiliares
{
    public static class MensagensPT
    {
        #region Mensagens de AVISO (412) PT-BR
        public const string a0002 = $"Nenhum usuário encontrado com este e-mail.";
        public const string a0007 = $"Código de autenticação já utilizado.";
        public const string a0008 = $"Código de autenticação expirado.";
        public const string a0010 = $"E-mail informado não é válido.";
        public const string a0011 = $"Nome do usuário não pode estar vazio.";
        public const string a0012 = $"E-mail do usuário não pode estar vazio.";
        public const string a0013 = $"E-mail já cadastrado no sistema.";
        public const string a0014 = $"Código de autenticação inválido.";
        public const string a0015 = $"Código de autenticação enviado para seu e-mail: ";
        #endregion

        #region Mensagens de ERRO (500) PT-BR
        public const string b0001 = $"Erro interno ao consultar o usuário.";
        public const string b0002 = $"Erro ao gerar código. Por favor tente novamente.";
        public const string b0003 = $"Código de Autenticação Inválido";
        public const string b0004 = $"Erro ao validar codigo de autenticação.";
        public const string b0006 = $"Erro ao autenticar código do usuário. Por favor, tente o processo novamente.";
        public const string b0007 = $"Erro ao atualizar dados do usuário. Por favor, tente novamente.";
        #endregion
    }
}
