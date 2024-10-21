import { FluentProvider, webDarkTheme } from "@fluentui/react-components";
import { ChatPage } from './pages/ChatPage';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const queryClient = new QueryClient()

function App() {

  return (
    <QueryClientProvider client={queryClient}>
      <FluentProvider theme={webDarkTheme}>
        <ChatPage />
      </FluentProvider>
    </QueryClientProvider>
  );
}

export default App
